// SemanticDetectors.cs
// Detectors on resolved symbols for the clock, the identifiers, the commit, the versions, the record keys and the folder references of the domain.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Topogenia.Architecture.Tests.SemanticSources;

namespace Topogenia.Architecture.Tests;

/// <summary>
/// Each detector reads the symbol that a name resolves to, so that a using static directive, an alias, a method group
/// or a partial name is seen as the member it designates. Each returns the places found, as "path:line".
/// </summary>
internal static class SemanticDetectors
{
    public const string WorkspaceFolder = DomainFolder + "Workspace/";

    /// <summary>D08: a DateTimeOffset, the local clock (DateTime.Now, Today), a local conversion or the local time of a time provider.</summary>
    public static IEnumerable<string> LocalDates(SourceFile file) =>
        Places(file, symbol => IsTypeOrMember(symbol, "System.DateTimeOffset")
            || IsMember(symbol, "System.DateTime", "Now", "Today", "ToLocalTime")
            || IsMember(symbol, "System.TimeProvider", "GetUtcNow", "GetLocalNow", "LocalTimeZone"));

    /// <summary>D07: a random identifier (Guid.NewGuid), however it is named.</summary>
    public static IEnumerable<string> RandomGuids(SourceFile file) => Places(file, symbol => IsMember(symbol, "System.Guid", "NewGuid"));

    /// <summary>
    /// D07, D08 in the domain: the clock, the identifier factory and any source of time or chance (DateTime.Now, UtcNow,
    /// Today, DateTimeOffset, Guid.NewGuid, CreateVersion7, Stopwatch, Random, RandomNumberGenerator, Environment.TickCount,
    /// TimeProvider). Only the operation context may use them.
    /// </summary>
    public static IEnumerable<string> ClockOrIdentifierFactories(SourceFile file) =>
        Places(file, symbol => IsMember(symbol, "System.DateTime", "Now", "UtcNow", "Today")
            || IsTypeOrMember(symbol, "System.DateTimeOffset")
            || IsMember(symbol, "System.Guid", "NewGuid", "CreateVersion7")
            || IsTypeOrMember(symbol, "System.Diagnostics.Stopwatch")
            || IsTypeOrMember(symbol, "System.Random")
            || IsTypeOrMember(symbol, "System.Security.Cryptography.RandomNumberGenerator")
            || IsMember(symbol, "System.Environment", "TickCount", "TickCount64")
            || IsTypeOrMember(symbol, "System.TimeProvider"));

    /// <summary>D06, AR-32: a reference to ProjectWorkspace.Commit, called or taken as a method group.</summary>
    public static IEnumerable<string> Commits(SourceFile file) =>
        Places(file, symbol => symbol is IMethodSymbol && IsMember(symbol, "Topogenia.Domain.Workspace.ProjectWorkspace", "Commit"));

    /// <summary>D06: a reference to the Stamp of a versioned record, called or taken as a method group.</summary>
    public static IEnumerable<string> Stamps(SourceFile file) =>
        Places(file, symbol => symbol is IMethodSymbol { Name: "Stamp" } method && IsVersionedRecord(method.ContainingType));

    /// <summary>
    /// D06, AR-32: a version or an update audit written by hand: an assignment of Version, UpdatedAt or UpdatedBy of a
    /// versioned record (in a with expression, an object initialiser or a statement), or a versioned record created with
    /// a version other than 1. The constant 1 starts the version line of a record whose key changes (a new record); the
    /// bodies of the Stamp methods of the model, which the workspace calls, are not hand writing.
    /// </summary>
    public static IEnumerable<string> VersionWrites(SourceFile file)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var assignment in file.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (file.SymbolOf(assignment.Left) is IPropertySymbol { Name: "Version" or "UpdatedAt" or "UpdatedBy" } property
                && IsVersionedRecord(property.ContainingType) && !InStamp(file, assignment) && !IsFirstVersion(file, property, assignment.Right))
                yield return file.Where(assignment);
        }
        foreach (var creation in file.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (file.Model.GetOperation(creation, token) is not IObjectCreationOperation { Constructor: { } constructor } operation
                || !IsVersionedRecord(constructor.ContainingType))
                continue;
            if (operation.Arguments.Any(argument => argument.Parameter?.Name == "Version" && argument.Value.ConstantValue is not { HasValue: true, Value: 1 }))
                yield return file.Where(creation);
        }
    }

    /// <summary>Version = 1: a record whose key changes is a new record and starts its version line again.</summary>
    private static bool IsFirstVersion(SourceFile file, IPropertySymbol property, ExpressionSyntax value) =>
        property.Name == "Version" && file.Model.GetConstantValue(value, TestContext.Current.CancellationToken) is { HasValue: true, Value: 1 };

    private static bool InStamp(SourceFile file, SyntaxNode node) =>
        file.Path.StartsWith(DomainFolder + "Model/", StringComparison.Ordinal)
        && node.Ancestors().OfType<MethodDeclarationSyntax>().Any(method => method.Identifier.ValueText == "Stamp");

    /// <summary>
    /// ARC4-M3: a record key built by hand: RecordKey.Of (called, taken as a method group or reached by using static or an
    /// alias), a creation of a RecordKey (new RecordKey(...) or a target-typed new) or a with expression on a RecordKey.
    /// Only the KeyOf method of each record of the model builds its key; every other file calls it.
    /// </summary>
    public static IEnumerable<string> RecordKeyBuilds(SourceFile file)
    {
        const string recordKey = "Topogenia.Domain.Model.RecordKey";
        foreach (var place in Places(file, symbol => symbol is IMethodSymbol && IsMember(symbol, recordKey, "Of")))
            yield return place;
        var token = TestContext.Current.CancellationToken;
        foreach (var node in file.Root.DescendantNodes().Where(node => node is BaseObjectCreationExpressionSyntax or WithExpressionSyntax))
        {
            if (file.Model.GetTypeInfo(node, token).Type is { } type && TypeOf(type) == recordKey)
                yield return $"{file.Where(node)} RecordKey";
        }
    }

    /// <summary>
    /// ARC4 minor 4: a reference to Versioning.Update, the stamping of a record kept outside a project workspace (the
    /// profile of a user, AR-07), called or taken as a method group.
    /// </summary>
    public static IEnumerable<string> VersioningUpdates(SourceFile file) =>
        Places(file, symbol => symbol is IMethodSymbol && IsMember(symbol, "Topogenia.Domain.Workspace.Versioning", "Update"));

    /// <summary>The domain folders that a file refers to, by any resolved name: using directives, partial or full names, members.</summary>
    public static IEnumerable<string> ReferencedFolders(SourceFile file) =>
        file.ResolvedNames().Select(pair => DomainFolderOf(pair.Symbol)).OfType<string>().Distinct(StringComparer.Ordinal);

    /// <summary>Each reference of a file to a domain folder, with its place: the folder and "path:line".</summary>
    public static IEnumerable<(string Folder, string Where)> FolderReferences(SourceFile file) =>
        file.ResolvedNames().Select(pair => (Folder: DomainFolderOf(pair.Symbol), Where: file.Where(pair.Name)))
            .Where(reference => reference.Folder is not null)
            .Select(reference => (reference.Folder!, reference.Where));

    /// <summary>The folder graph of the domain from its compiled files: folder of a file to the folders it refers to.</summary>
    public static Dictionary<string, HashSet<string>> FolderGraph(IEnumerable<SourceFile> files)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var parts = file.DomainPath.Split('/');
            if (parts.Length < 2)
                continue;
            if (!graph.TryGetValue(parts[0], out var targets))
                graph[parts[0]] = targets = new HashSet<string>(StringComparer.Ordinal);
            targets.UnionWith(ReferencedFolders(file).Where(folder => folder != parts[0]));
        }
        return graph;
    }

    private static IEnumerable<string> Places(SourceFile file, Func<ISymbol, bool> forbidden) =>
        file.ResolvedNames().Where(pair => forbidden(pair.Symbol)).Select(pair => $"{file.Where(pair.Name)} {pair.Symbol.Name}");

    /// <summary>The rule applied to the files of a folder, except the files allowed to break it.</summary>
    public static IReadOnlyList<string> Offenders(IEnumerable<SourceFile> files, Func<SourceFile, IEnumerable<string>> detector, params string[] allowed) =>
        files.Where(file => !allowed.Any(prefix => file.Path.StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(detector)
            .ToList();
}
