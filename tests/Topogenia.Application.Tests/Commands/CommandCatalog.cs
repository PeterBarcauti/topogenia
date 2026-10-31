// CommandCatalog.cs
// The table of the rights of the commands and queries, one partial file per feature (read by CommandCatalogTests).

namespace Topogenia.Application.Tests.Commands;

/// <summary>
/// Each partial file of a feature holds one static table `Dictionary&lt;Type, string&gt;`: the command or query type and
/// the right of its route in annex A 1 (`reader`, `contributor`, `facilitator`, `sheet_fill`, `admin`).
/// </summary>
internal static partial class CommandCatalog
{
}
