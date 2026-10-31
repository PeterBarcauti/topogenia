// ImportPreparation.cs
// What an import writes, prepared without writing: the users of its logins, its records, the importer facilitator, its consistency.

using Topogenia.Application.Commands;
using Topogenia.Application.Ports;
using Topogenia.Domain.Errors;
using Topogenia.Domain.Model;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;
using Topogenia.Domain.Workspace;

namespace Topogenia.Application.Exchange;

/// <summary>
/// An import ready to be written: its project, its root map, the users to create first, its records, what it settled and
/// the translations of the package that it leaves out (AR-119), so that the preview counts what the import writes.
/// </summary>
internal sealed record PreparedImport(Project Project, Guid RootMapId, int Members, IReadOnlyList<AppUser> NewUsers, IReadOnlyList<RecordChange> Changes,
    ImportSettlement Settlement, int IgnoredTranslations);

/// <summary>
/// The part common to the preview and to the import (spec 7.3: the preview is a complete validation without writing; the
/// import does it again, AR-38). The logins of the package are looked up in the database, and those nobody has are given a
/// user as a member invited would be (`UserRules.NewUser`, the name of the directory, else the name the package gives a
/// member); only the users that the rebuilt records name are kept for creation. The project, rebuilt under
/// <c>code</c>, takes a language of origin configured here (LANGUE_INVALIDE otherwise), the importer becomes facilitator
/// and the holders who are not members are released (`ImportedProjects.Settle`); a value that the rule of its entry
/// would refuse is refused PAQUET_INVALIDE, reason <c>values</c>, with the names of the fields (`ImportedRecords`), and a
/// project that breaks a rule of the structure, reason <c>structure</c>, with the names of the rules (ADR 052).
/// </summary>
internal static class ImportPreparation
{
    /// <summary>
    /// <paramref name="replaced"/> is the project that the import replaces, none for a new project: the imported project
    /// keeps its classification (AR-117) and its members when the import brings none (AR-115).
    /// </summary>
    public static async Task<PreparedImport> PrepareAsync(UserScope scope, IPackageImporter importer, IReadPackage package, string code, bool withMembers,
        ProjectWorkspace? replaced, CancellationToken cancellationToken)
    {
        var candidates = await CandidatesAsync(scope, package, cancellationToken);
        var newUsers = new Dictionary<Guid, AppUser>();
        var records = importer.Build(package, new ImportPlan(scope.Operation, code, login =>
        {
            if (!candidates.TryGetValue(Login.Normalize(login), out var candidate))
                return null;
            if (candidate.New)
                newUsers.TryAdd(candidate.User.Id, candidate.User);
            return candidate.User.Id;
        }, withMembers));
        var project = (Project)records[0];
        scope.Languages.Require(project.SourceLanguage, "source_language");
        if (replaced is not null)
            project = ImportedProjects.KeepClassification(project, replaced.Project);
        var workspace = new ProjectWorkspace(project.Id, [project]);
        foreach (var record in records.Skip(1))
            workspace.Put(record);
        var ignored = ImportedRecords.IgnoreTranslations(workspace, scope.Languages);
        if (ImportedRecords.Violations(workspace, scope.Languages) is { Count: > 0 } fields)
            throw ImportedRecords.Refusal(fields);
        if (replaced is not null)
            ImportedProjects.KeepMembers(workspace, replaced.All<ProjectMember>());
        var settlement = ImportedProjects.Settle(workspace, scope.Operation);
        var violations = ImportedProjects.Violations(workspace);
        if (violations.Count > 0)
            throw new DomainException(ErrorCodes.InvalidPackage, RefusalKind.Invalid, new Dictionary<string, object?>
            {
                ["reason"] = "structure",
                ["rules"] = violations.Select(violation => violation.Split(':')[0]).Distinct(StringComparer.Ordinal).ToList(),
            });
        var rootMapId = workspace.RootMap.Id;
        var members = workspace.All<ProjectMember>().Count();
        var changes = workspace.Commit(scope.Operation);
        return new PreparedImport(project, rootMapId, members, [.. newUsers.Values], [new RecordChange(ChangeKind.Created, null, project), .. changes.Changes],
            settlement, ignored);
    }

    /// <summary>The logins of the package that no user of the database has, by login as the package writes them.</summary>
    public static async Task<IReadOnlyList<string>> UnknownLoginsAsync(UserScope scope, IReadPackage package, CancellationToken cancellationToken)
    {
        var known = await KnownAsync(scope, package, cancellationToken);
        return [.. package.Logins.Where(login => !known.ContainsKey(Login.Normalize(login)))];
    }

    /// <summary>The users of the logins of the package: those of the database, and a new user for each other login.</summary>
    private static async Task<Dictionary<string, (AppUser User, bool New)>> CandidatesAsync(UserScope scope, IReadPackage package,
        CancellationToken cancellationToken)
    {
        var candidates = (await KnownAsync(scope, package, cancellationToken)).ToDictionary(pair => pair.Key, pair => (pair.Value, false), StringComparer.Ordinal);
        var names = package.Members.GroupBy(member => Login.Normalize(member.Login), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().DisplayName, StringComparer.Ordinal);
        foreach (var login in package.Logins.Where(login => !candidates.ContainsKey(Login.Normalize(login))))
        {
            var entry = await scope.Ports.Directory.FindAsync(login, cancellationToken);
            if (entry?.DisplayName is null && names.TryGetValue(Login.Normalize(login), out var name))
                entry = new DirectoryEntry(name, entry?.Email);
            var user = UserRules.NewUser(scope.Operation, login, entry, scope.Ports.Administrators.Logins);
            candidates[user.LoginNormalized] = (user, true);
        }
        return candidates;
    }

    private static async Task<Dictionary<string, AppUser>> KnownAsync(UserScope scope, IReadPackage package, CancellationToken cancellationToken)
    {
        var found = package.Logins.Count == 0 ? [] : await scope.Ports.Users.FindByLoginsAsync([.. package.Logins], cancellationToken);
        return found.ToDictionary(user => user.LoginNormalized, StringComparer.Ordinal);
    }
}
