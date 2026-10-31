// SingleImplementationRules.cs
// Rules of the domain that have a single implementation, recognised on resolved symbols: sub-map walks, block ends, orders, codes, RG-C05, texts.

namespace Topogenia.Architecture.Tests;

/// <summary>
/// Each rule below was once written several times with different results (gates of phase 2, rounds 2 and 3). Its
/// detector (<see cref="SingleImplementationDetectors"/>) reads the compiled domain, so that a partial name, an alias, a
/// literal written by hand or another variable name does not hide a copy. The rule may be found only in the files of its
/// single implementation, and must be found there (positive control). A file of another folder still waiting for its owner
/// to call the single implementation is listed as pending: the test fails as soon as the rule leaves it.
/// </summary>
public sealed class SingleImplementationRules
{
    private static readonly Dictionary<string, Func<SourceFile, IEnumerable<string>>> Detectors = new(StringComparer.Ordinal)
    {
        ["walk up"] = SingleImplementationDetectors.WalkUp,
        ["walk down"] = SingleImplementationDetectors.WalkDown,
        ["block ends"] = SingleImplementationDetectors.BlockEnds,
        ["lane order"] = SingleImplementationDetectors.LaneOrder,
        ["document entry order"] = SingleImplementationDetectors.DocumentEntryOrder,
        ["document key"] = SingleImplementationDetectors.DocumentKey,
        ["sheet lookup"] = SingleImplementationDetectors.SheetLookup,
        ["macro choice"] = SingleImplementationDetectors.MacroChoiceRefusal,
        ["code literals"] = SingleImplementationDetectors.CodeLiterals,
        ["flow link rule"] = SingleImplementationDetectors.FlowLinkRule,
        ["origin language"] = SingleImplementationDetectors.OriginLanguage,
        ["map bears text"] = SingleImplementationDetectors.MapBearsText,
    };

    public static TheoryData<string, string, string[], string[]> Rules() => new()
    {
        // ARC-M6: the depth, the breadcrumb and the reading path read ProjectWorkspace.ParentSteps.
        { "walk up the sub-maps", "walk up", ["Workspace/ProjectWorkspace.Queries.cs"], [] },
        // ARC-M4: ProjectWorkspace.Subtree and SubMapSubtrees.
        { "walk down the sub-maps", "walk down", ["Workspace/ProjectWorkspace.Queries.cs"], [] },
        // ARC-M5: BlockEnds.Of, used by the anchored insertion and by the ends of a dissolved detail.
        { "entries and exits of a block", "block ends", ["Blocks/BlockEnds.cs"], [] },
        // ARC3-M2: ProjectWorkspace.InLaneOrder (the 1.x NormaliserOrdreCouloirs), for the RACI and the outputs too.
        { "order of the lanes", "lane order", ["Workspace/ProjectWorkspace.Queries.cs"], [] },
        { "entry order of documents", "document entry order", ["Documents/DocumentRules.cs"], [] },
        // ARC4-M3: Document.KeyOf, called by Document.Key and by ProjectWorkspace.Document.
        { "key of a document", "document key", ["Model/SheetRecords.cs"], [] },
        { "sheet of a step", "sheet lookup", ["Sheets/SheetQueries.cs"], [] },
        // ARC-M3: MacroStepDeletion.SettleSubMaps.
        { "choice of RG-C09", "macro choice", ["SubMaps/MacroStepDeletion.cs"], [] },
        // ARC3-M3: an error or warning code is written only as its constant.
        { "error and warning codes", "code literals", ["Errors/ErrorCodes.cs"], [] },
        // RG-C05: FlowLinks, called by the creation of a flow, the invariants and the undo.
        { "RG-C05 of a flow", "flow link rule", ["Workspace/FlowLinks.cs"], [] },
        // ARC3-M5, ADR 026: LocalizedText.TakesTranslationIn.
        { "language of origin of a translation", "origin language", ["Values/LocalizedText.cs"], [] },
        // ARC3 minor 5: TranslationRules.BearsText (relabel of a step, export and import of translations).
        { "map that bears the text of its step or project", "map bears text", ["Translations/TranslationRules.cs"], [] },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void A_rule_with_a_single_implementation_is_written_only_in_its_file(string rule, string detector, string[] owners, string[] pending)
    {
        CoreSources.Require();
        var found = SemanticSources.Domain.Select(file => (file.DomainPath, Places: Detectors[detector](file).ToList()))
            .Where(file => file.Places.Count > 0)
            .ToList();
        var holders = found.Select(file => file.DomainPath).ToList();
        var copies = found.Where(file => !owners.Contains(file.DomainPath) && !pending.Contains(file.DomainPath)).SelectMany(file => file.Places);

        Assert.True(holders.All(holder => owners.Contains(holder) || pending.Contains(holder)), $"{rule}: written again in {string.Join(", ", copies)}");
        Assert.True(owners.All(holders.Contains), $"{rule}: the single implementation left {string.Join(", ", owners.Except(holders))}");
        Assert.True(pending.All(holders.Contains),
            $"{rule}: {string.Join(", ", pending.Except(holders))} no longer writes the rule; remove it from the pending files");
    }

    private static readonly Dictionary<string, Func<SourceFile, IEnumerable<MemberPlace>>> ReadDetectors = new(StringComparer.Ordinal)
    {
        ["lane sort order"] = SingleReadDetectors.LaneSortOrderReads,
        ["parent step of a map"] = SingleReadDetectors.ParentStepReads,
        ["sequence of sheets"] = SingleReadDetectors.SheetSequences,
    };

    /// <summary>
    /// ARC4-M1: members of the model read only by the members listed ("Folder/File.cs:Member"), whatever the shape of the
    /// code: every read is found, not only a loop or an ordering. A write is not a read. A pending member still holds a
    /// copy that its owner must replace by a call of the single implementation; the test fails as soon as it leaves it.
    /// </summary>
    public static TheoryData<string, string, string[], string[]> SingleReaders() => new()
    {
        // InLaneOrder sorts; Add makes room for the new lane, ApplyOrder and NormaliseLaneOrder renumber (writes that read
        // the number to skip an unchanged lane).
        {
            "order of the lanes", "lane sort order",
            ["Workspace/ProjectWorkspace.Queries.cs:InLaneOrder", "Lanes/LaneOperations.cs:Add", "Lanes/LaneOperations.cs:ApplyOrder",
                "Structure/StructureEdits.cs:NormaliseLaneOrder"],
            []
        },
        // ParentSteps (the only walk up) and SubMapOf (the only search down) in the queries; IsRoot in the model; the
        // invariants of a sub-map; the purge, which follows deleted maps too; and two reads of one level, never a walk:
        // the frequency of the parent step of a dissolved map, the parent step whose label a map may bear in the catalogue.
        {
            "parent step of a map", "parent step of a map",
            ["Workspace/ProjectWorkspace.Queries.cs:ParentSteps", "Workspace/ProjectWorkspace.Queries.cs:SubMapOf", "Model/StructureRecords.cs:IsRoot",
                "Workspace/StructureInvariants.cs:AddMapAndLaneViolations", "Trash/PurgeScope.cs:Complete", "Trash/PurgeScope.cs:AddMapsBottomUp",
                "Dissolutions/DissolutionFrequencies.cs:Ensure", "Translations/TranslationCatalog.cs:Texts"],
            []
        },
        // SheetOf; the purge, which removes the sheets of every purged step; the check of an imported project, which reads
        // the values of every sheet it carries and never looks for the sheet of a step (ADR 052).
        { "sheet of a step", "sequence of sheets", ["Sheets/SheetQueries.cs:SheetOf", "Trash/PurgeScope.cs:AddSheetsAndDocuments",
            "Projects/ImportedRecords.cs:Sheets"], [] },
    };

    [Theory]
    [MemberData(nameof(SingleReaders))]
    public void A_member_of_the_model_is_read_only_by_its_single_implementation(string rule, string detector, string[] owners, string[] pending)
    {
        CoreSources.Require();
        var places = SemanticSources.Domain.SelectMany(ReadDetectors[detector]).ToList();
        var holders = places.Select(place => place.Owner).Distinct(StringComparer.Ordinal).ToList();
        var copies = places.Where(place => !owners.Contains(place.Owner) && !pending.Contains(place.Owner))
            .Select(place => $"{place.Where} ({place.Owner})").Distinct(StringComparer.Ordinal);

        Assert.True(holders.All(holder => owners.Contains(holder) || pending.Contains(holder)), $"{rule}: read again in {string.Join(", ", copies)}");
        Assert.True(owners.All(holders.Contains), $"{rule}: the single implementation left {string.Join(", ", owners.Except(holders))}");
        Assert.True(pending.All(holders.Contains),
            $"{rule}: {string.Join(", ", pending.Except(holders))} no longer reads it; remove it from the pending members");
    }
}
