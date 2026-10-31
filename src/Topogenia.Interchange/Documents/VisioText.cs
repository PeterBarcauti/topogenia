// VisioText.cs
// The text of a Visio drawing (.vsdx): each page, its shapes with their text, their master, their place and size, and the links its connectors draw between them (spec 3.0 section 3.4, lot 4).

using System.Globalization;
using System.Text;
using System.Xml;

namespace Topogenia.Interchange.Documents;

/// <summary>
/// Reads a Visio drawing: its foreground pages in their order (`visio/pages/pages.xml` and its relations), the names of
/// its masters (`visio/masters/masters.xml`: Process, Decision, Swimlane...), then on each page its shapes and its
/// connections (`Connect`: the begin and the end of a connector glued to a shape). A page is written as its shapes that
/// carry a text or a link (their identifier, text, master, centre and size in inches, the centre of a shape of a group
/// placed on the page), then its links from the begin of each connector to its end, with the text of the connector.
/// A model reads the lanes of a cross-functional flowchart from the shapes of its lanes and their places.
/// </summary>
internal static class VisioText
{
    private const string Vocabulary = "visio";

    public static bool Read(OfficeArchive archive, BoundedText text)
    {
        var document = archive.Main(OfficeArchive.VisioDocument, "visio/document.xml");
        var parts = archive.Relations(document);
        var pages = archive.Target(parts, "/pages") ?? "visio/pages/pages.xml";
        var masters = Masters(archive, archive.Target(parts, "/masters") ?? "visio/masters/masters.xml");
        var list = Pages(archive, pages);
        if (list is null)
            return false;
        var targets = archive.Relations(pages);
        foreach (var (name, id) in list)
        {
            if (text.Full)
                break;
            if (targets.TryGetValue(id, out var target))
                Page(archive, target.Part, name, masters, text);
        }
        return true;
    }

    /// <summary>The names of the masters by their identifier, the universal name first (the same in every language of Visio).</summary>
    private static Dictionary<string, string> Masters(OfficeArchive archive, string part)
    {
        var masters = new Dictionary<string, string>(StringComparer.Ordinal);
        using var xml = archive.Xml(part);
        if (xml is null)
            return masters;
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.Element && OfficeArchive.In(xml, Vocabulary) && xml.LocalName == "Master" && xml.GetAttribute("ID") is { } id)
                masters[id] = xml.GetAttribute("NameU") ?? xml.GetAttribute("Name") ?? "";
        }
        return masters;
    }

    /// <summary>The foreground pages in their order (name, relation); null without the list of the pages.</summary>
    private static List<(string Name, string Id)>? Pages(OfficeArchive archive, string part)
    {
        using var xml = archive.Xml(part);
        if (xml is null)
            return null;
        var pages = new List<(string, string)>();
        string? name = null;
        var background = false;
        while (xml.Read())
        {
            if (xml.NodeType != XmlNodeType.Element || !OfficeArchive.In(xml, Vocabulary))
                continue;
            if (xml.LocalName == "Page")
            {
                name = xml.GetAttribute("Name") ?? xml.GetAttribute("NameU") ?? "";
                background = xml.GetAttribute("Background") is "1" or "true";
            }
            else if (xml.LocalName == "Rel" && name is not null && !background && OfficeArchive.RelationOf(xml) is { } id)
                pages.Add((name, id));
        }
        return pages;
    }

    private static void Page(OfficeArchive archive, string part, string name, Dictionary<string, string> masters, BoundedText text)
    {
        var (shapes, connects) = Contents(archive, part);
        var connectors = connects.GroupBy(connect => connect.From).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var linked = connects.Select(connect => connect.To).ToHashSet(StringComparer.Ordinal);
        var byId = shapes.ToDictionary(shape => shape.Id, StringComparer.Ordinal);
        text.EndLine().Append("\n## Page \"").Append(name).Append("\"\n");
        text.Append("Shapes (identifier, text, master, centre x and y and size in inches, y growing upwards):\n");
        foreach (var shape in shapes.Where(shape => !connectors.ContainsKey(shape.Id) && (shape.Text.Length > 0 || linked.Contains(shape.Id))))
        {
            if (text.Full)
                return;
            text.Append("- ").Append(Name(shape)).Append(Kind(shape, masters));
            if (shape.Centre() is { } centre)
                text.Append(" at (").Append(Number(centre.X)).Append(", ").Append(Number(centre.Y)).Append(')');
            if (shape.Width is { } width && shape.Height is { } height)
                text.Append(" size ").Append(Number(width)).Append(" x ").Append(Number(height));
            text.Append('\n');
        }
        if (connectors.Count == 0)
            return;
        text.Append("Links (from the begin of a connector to its end):\n");
        foreach (var (connector, ends) in connectors)
        {
            var begin = ends.FirstOrDefault(end => end.Cell.StartsWith("Begin", StringComparison.Ordinal))?.To;
            var end = ends.FirstOrDefault(end => end.Cell.StartsWith("End", StringComparison.Ordinal))?.To;
            text.Append("- ").Append(begin is null ? "(not glued)" : Name(byId.GetValueOrDefault(begin), begin)).Append(" -> ")
                .Append(end is null ? "(not glued)" : Name(byId.GetValueOrDefault(end), end));
            if (byId.GetValueOrDefault(connector) is { Text.Length: > 0 } labelled)
                text.Append(" : \"").Append(labelled.Text).Append('"');
            text.Append('\n');
        }
    }

    private static string Name(Shape? shape, string? id = null) =>
        shape is null ? id ?? "" : shape.Text.Length > 0 ? $"{shape.Id} \"{shape.Text}\"" : $"{shape.Id} (no text)";

    /// <summary>The master of a shape in brackets, or its own name without the number Visio adds to it; nothing for a plain sheet.</summary>
    private static string Kind(Shape shape, Dictionary<string, string> masters)
    {
        var kind = shape.Master is { } master && masters.TryGetValue(master, out var named) ? named : shape.Name;
        if (kind is null)
            return shape.Group ? " [group]" : "";
        var dot = kind.LastIndexOf('.');
        if (dot > 0 && kind[(dot + 1)..].All(char.IsAsciiDigit))
            kind = kind[..dot];
        return kind.StartsWith("Sheet", StringComparison.Ordinal) ? (shape.Group ? " [group]" : "") : $" [{kind}]";
    }

    private static string Number(double value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The shapes of a page, those of its groups with them, in the order of the page, and its connections.</summary>
    private static (List<Shape> Shapes, List<Connect> Connects) Contents(OfficeArchive archive, string part)
    {
        var shapes = new List<Shape>();
        var connects = new List<Connect>();
        using var xml = archive.Xml(part);
        if (xml is null)
            return (shapes, connects);
        var open = new Stack<Shape>();
        var parents = new Stack<string>();
        var order = 0;
        while (xml.Read())
        {
            if (xml.NodeType == XmlNodeType.EndElement && OfficeArchive.In(xml, Vocabulary))
            {
                parents.TryPop(out _);
                if (xml.LocalName == "Shape" && open.TryPop(out var closed))
                    shapes.Add(closed);
                continue;
            }
            if (xml.NodeType != XmlNodeType.Element || !OfficeArchive.In(xml, Vocabulary))
                continue;
            var parent = parents.TryPeek(out var above) ? above : null;
            switch (xml.LocalName)
            {
                case "Shape":
                    var shape = new Shape(xml.GetAttribute("ID") ?? "", xml.GetAttribute("NameU") ?? xml.GetAttribute("Name"), xml.GetAttribute("Master"),
                        xml.GetAttribute("Type") == "Group", open.TryPeek(out var group) ? group : null, order++);
                    if (xml.IsEmptyElement)
                    {
                        shapes.Add(shape);
                        continue;
                    }
                    open.Push(shape);
                    break;
                case "Cell" when parent == "Shape" && open.TryPeek(out var owner):
                    owner.Set(xml.GetAttribute("N"), xml.GetAttribute("V"));
                    break;
                case "Text" when parent == "Shape" && open.TryPeek(out var holder) && !xml.IsEmptyElement:
                    holder.Text = TextOf(xml);
                    continue;
                case "Connect" when xml.GetAttribute("FromSheet") is { } from && xml.GetAttribute("ToSheet") is { } to:
                    connects.Add(new Connect(from, xml.GetAttribute("FromCell") ?? "", to));
                    break;
            }
            if (!xml.IsEmptyElement)
                parents.Push(xml.LocalName);
        }
        shapes.Sort((left, right) => left.Order.CompareTo(right.Order));
        return (shapes, connects);
    }

    /// <summary>The text of a shape, its lines and its fields joined by spaces, read to its end.</summary>
    private static string TextOf(XmlReader xml)
    {
        var text = new StringBuilder();
        using (var inner = xml.ReadSubtree())
        {
            while (inner.Read())
            {
                if (inner.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace or XmlNodeType.CDATA)
                    text.Append(inner.Value);
            }
        }
        return string.Join(' ', text.ToString().Split((char[])['\n', '\r', '\t', ' ', '\u00A0', '\u2028', '\u2029'], StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>A glue of a connector: the cell of the connector (BeginX, EndX) and the shape it is glued to.</summary>
    private sealed record Connect(string From, string Cell, string To);

    /// <summary>A shape of a page: its cells of place and size, its text, the group it belongs to.</summary>
    private sealed class Shape(string id, string? name, string? master, bool group, Shape? parent, int order)
    {
        public string Id { get; } = id;
        public string? Name { get; } = name;
        public string? Master { get; } = master;
        public bool Group { get; } = group;
        public string Text { get; set; } = "";
        public double? Width { get; private set; }
        public double? Height { get; private set; }

        /// <summary>The order the shape was opened in, the order of the page (a group before its shapes).</summary>
        public int Order { get; } = order;

        private double? pinX, pinY, locPinX, locPinY;

        public void Set(string? cell, string? value)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                return;
            switch (cell)
            {
                case "PinX": pinX = number; break;
                case "PinY": pinY = number; break;
                case "Width": Width = number; break;
                case "Height": Height = number; break;
                case "LocPinX": locPinX = number; break;
                case "LocPinY": locPinY = number; break;
            }
        }

        /// <summary>The centre of the shape on the page: its pin, moved by the origin of its group (the rotations are not followed); null when a cell is inherited.</summary>
        public (double X, double Y)? Centre()
        {
            if (pinX is not { } x || pinY is not { } y)
                return null;
            if (parent is null)
                return (x, y);
            if (parent.Centre() is not { } origin || parent.Width is not { } width || parent.Height is not { } height)
                return null;
            return (origin.X - (parent.locPinX ?? width / 2) + x, origin.Y - (parent.locPinY ?? height / 2) + y);
        }
    }
}
