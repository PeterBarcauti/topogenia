// ProjectApi.cs
// The routes of the projects turned into their requests: create, list, detail, label of the sharing group, classification, glossary of the translations (annex A 1 projets, A 2.1, spec 4.1).

using Topogenia.Application.Commands;
using Topogenia.Application.Projects;
using Topogenia.Application.Queries;
using Topogenia.Domain.Projects;
using Topogenia.Domain.Values;

namespace Topogenia.Host.Api.Projects;

/// <summary>A member entered in a list (1.x `MembreDemande`: `login`, `role`, `est_sponsor`).</summary>
public sealed record MemberBody(string? Login, string? Role, bool IsSponsor, bool IsKeyContributor);

/// <summary>`POST /api/projects` (1.x `ProjetDemande`).</summary>
public sealed record CreateProjectBody(
    string? Code,
    string? Name,
    string? SourceLanguage,
    Dictionary<string, string?>? Translations,
    string? Description,
    string? SharedFolder,
    string? Classification,
    string? SharingGroupLabel,
    List<MemberBody>? Members);

/// <summary>`PUT /api/projects/{id}/sharing-group-label` (AR-64, route named by the host): the label, or null to remove it.</summary>
public sealed record SharingGroupLabelBody(string? Label);

/// <summary>
/// `PUT /api/projects/{id}/classification` (spec 4.1, route named by the host): the code of the level, required (spec 5.4:
/// an absent field is an error, never the default of the creation).
/// </summary>
public sealed record ClassificationBody(string? Classification);

/// <summary>`PUT /api/projects/{id}/translation-glossary` (ADR 065, route named by the host): the glossary, or null to remove it.</summary>
public sealed record TranslationGlossaryBody(string? Glossary);

/// <summary>`PUT /api/projects/{id}/origin-language` (3.2, ADR 100): the language that becomes the language of origin of the project.</summary>
public sealed record OriginLanguageBody(string? Language);

/// <summary>`PUT /api/projects/{id}/work-time` (3.0, ADR 085): the working day and the hours of a full-time equivalent in a month; an absent value is kept.</summary>
public sealed record WorkTimeBody(decimal? WorkdayHours, decimal? FteHoursPerMonth);

/// <summary>Reads the bodies of the project routes into the requests of the domain and runs them.</summary>
public sealed class ProjectApi
{
    private readonly UserRunner users;
    private readonly QueryRunner queries;
    private readonly CommandRunner commands;
    private readonly LanguageSettings languages;

    public ProjectApi(UserRunner users, QueryRunner queries, CommandRunner commands, LanguageSettings languages)
    {
        this.users = users;
        this.queries = queries;
        this.commands = commands;
        this.languages = languages;
    }

    public static IReadOnlyList<MemberEntry>? Members(IEnumerable<MemberBody>? members) =>
        members?.Select(member => new MemberEntry(member.Login, member.Role, member.IsSponsor, member.IsKeyContributor)).ToList();

    public Task<CreatedProject> CreateAsync(CreateProjectBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var request = new CreateProjectRequest(body.Code, body.Name, body.SourceLanguage, body.Translations, body.Description, body.SharedFolder,
            body.Classification, body.SharingGroupLabel, Members(body.Members));
        return users.RunAsync(new CreateProjectCommand(request), cancellationToken);
    }

    public Task<IReadOnlyList<ProjectListItem>> ListAsync(CancellationToken cancellationToken) =>
        users.RunAsync(new ListProjectsQuery(), cancellationToken);

    public Task<ProjectDetail> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        queries.RunAsync(new GetProjectQuery(projectId), cancellationToken);

    /// <summary>Changes the label, then reads the project again (the command touches no map).</summary>
    public async Task<ProjectDetail> ChangeSharingGroupLabelAsync(Guid projectId, SharingGroupLabelBody body, string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(ChangeSharingGroupLabelCommand.WithVersion(projectId, body.Label, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>Changes the classification, then reads the project again (the command touches no map).</summary>
    public async Task<ProjectDetail> ChangeClassificationAsync(Guid projectId, ClassificationBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(ChangeClassificationCommand.WithVersion(projectId, body.Classification, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>Changes the glossary of the translations, then reads the project again (the command touches no map).</summary>
    public async Task<ProjectDetail> ChangeTranslationGlossaryAsync(Guid projectId, TranslationGlossaryBody body, string? ifMatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(ChangeTranslationGlossaryCommand.WithVersion(projectId, body.Glossary, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>Changes the language of origin, then reads the project again: its name and its language are the new ones (the command names no map).</summary>
    public async Task<ProjectDetail> ChangeOriginLanguageAsync(Guid projectId, OriginLanguageBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(ChangeOriginLanguageCommand.WithVersion(projectId, body.Language, languages, ApiInput.Version(ifMatch)), cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }

    /// <summary>Changes the work time of the value stream, then reads the project again (the command touches no map).</summary>
    public async Task<ProjectDetail> ChangeWorkTimeAsync(Guid projectId, WorkTimeBody body, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await commands.RunAsync(ChangeWorkTimeCommand.WithVersion(projectId, body.WorkdayHours, body.FteHoursPerMonth, ApiInput.Version(ifMatch)),
            cancellationToken);
        return await GetAsync(projectId, cancellationToken);
    }
}
