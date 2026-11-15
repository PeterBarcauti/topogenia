// ExportFileName.cs
// The name of an exported file (A-1.2-16, 1.x Codes.NomDossier): the code, a short name without accents, the day.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Topogenia.Interchange.Files;

/// <summary>
/// The rule of the 1.x export (`ServiceFichiers.cs:45`, `Codes.NomDossier`): <c>CODE_Short-name_yyyy-mm-dd.ext</c>. The
/// short name keeps the letters and digits without their accents, turns spaces, dashes and underscores into one dash,
/// writes the ligatures out (oe, ae, ss), drops any other character and keeps at most 30 characters; an empty short name
/// leaves the code alone. The day is the UTC day of the export (D08; the 1.x took the local day).
/// </summary>
public static partial class ExportFileName
{
    public const int ShortNameMax = 30;

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedDashes();

    public static string Of(string code, string name, DateTime exportedAt, string extension) =>
        ShortName(code, name) + "_" + exportedAt.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture) + extension;

    /// <summary>The code and the short name of a label, joined by an underscore (the 1.x suggested folder name).</summary>
    public static string ShortName(string code, string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        var kept = new StringBuilder();
        foreach (var c in label.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
                kept.Append(c);
            else if (c is ' ' or '-' or '_')
                kept.Append('-');
            else
                kept.Append(Ligature(c));
        }
        var shortName = RepeatedDashes().Replace(kept.ToString(), "-").Trim('-');
        if (shortName.Length > ShortNameMax)
            shortName = shortName[..ShortNameMax].TrimEnd('-');
        return shortName.Length == 0 ? code : code + "_" + shortName;
    }

    private static string Ligature(char c) => c switch
    {
        'œ' => "oe",
        'Œ' => "OE",
        'æ' => "ae",
        'Æ' => "AE",
        'ß' => "ss",
        _ => "",
    };
}
