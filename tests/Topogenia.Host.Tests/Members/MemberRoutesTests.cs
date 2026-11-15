// MemberRoutesTests.cs
// The routes of the members of a project through the whole host on both databases: replace the list, participants (annex A 1, A 2.1).

using System.Text.Json;
using Topogenia.Host.Tests.Api;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Members;

public sealed class MemberRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Invited = @"TEST\nina";

    private const string NeverInvited = @"TEST\zoe";

    private async Task<ApiProject> ProjectAsync(EngineKind kind) => await ApiProject.CreateAsync(await hosts.OfAsync(kind));

    private static string Members(ApiProject project) => $"/api/projects/{project.Id}/members";

    private static string Participants(ApiProject project) => $"/api/projects/{project.Id}/participants";

    private static List<string?> LoginsOf(JsonElement members) => [.. members.EnumerateArray().Select(member => member.GetProperty("login").GetString())];

    private static Guid UserId(JsonElement members, string login) =>
        members.EnumerateArray().Single(member => member.GetProperty("login").GetString() == login).GetProperty("user_id").GetGuid();

    private static IEnumerable<Guid> Ids(JsonElement ids) => ids.EnumerateArray().Select(id => id.GetGuid());

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_whole_list_replaces_the_members_with_an_invited_user_and_the_participants_read_it_back(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        var before = (await facilitator.GetAsync(Participants(project))).Ok();
        var replaced = (await facilitator.PutAsync(Members(project), new object[]
        {
            new { login = Logins.Contributor, role = "contributor", is_sponsor = false, is_key_contributor = true },
            new { login = Invited, role = "reader", is_sponsor = false },
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
        }, await project.ProjectVersionAsync())).Ok();

        var members = replaced.GetProperty("members");
        Assert.Equal([Logins.Facilitator, Logins.Contributor, Invited], LoginsOf(members));
        var paul = members[1];
        Assert.Equal(("contributor", true, 0), (paul.GetProperty("role").GetString(), paul.GetProperty("is_key_contributor").GetBoolean(), paul.GetProperty("display_order").GetInt32()));
        Assert.Equal(("nina", "reader"), (members[2].GetProperty("display_name").GetString(), members[2].GetProperty("role").GetString()));
        Assert.Equal([UserId(members, Invited)], Ids(replaced.GetProperty("invited_user_ids")));
        Assert.Equal(new[] { UserId(before, Logins.Admin), UserId(before, Logins.Reader) }.Order(), Ids(replaced.GetProperty("removed_user_ids")).Order());

        var participants = (await contributor.GetAsync(Participants(project))).Ok();
        Assert.Equal(LoginsOf(members), LoginsOf(participants));
        Assert.Equal(JsonValueKind.Array, participants.ValueKind);
        (await reader.GetAsync(Participants(project))).Refused(404, "INTROUVABLE");
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task Only_a_facilitator_replaces_the_members_a_reader_reads_them_and_a_non_member_sees_nothing(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var contributor = project.Contributor;
        using var reader = project.Reader;
        using var stranger = project.Stranger;
        var list = new object[] { new { login = Logins.Contributor, role = "facilitator", is_sponsor = false } };
        (await contributor.PutAsync(Members(project), list)).Refused(403, "ACCES_REFUSE");
        (await stranger.PutAsync(Members(project), list)).Refused(404, "INTROUVABLE");
        (await stranger.GetAsync(Participants(project))).Refused(404, "INTROUVABLE");

        var participants = (await reader.GetAsync(Participants(project))).Ok();
        Assert.Equal([Logins.Facilitator, Logins.Admin, Logins.Contributor, Logins.Reader], LoginsOf(participants));
        Assert.Equal([true, false, false, false], participants.EnumerateArray().Select(member => member.GetProperty("is_sponsor").GetBoolean()));
        Assert.Equal(["facilitator", "facilitator", "contributor", "reader"], participants.EnumerateArray().Select(member => member.GetProperty("role").GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_refused_list_changes_nothing_and_invites_nobody(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        var url = Members(project);
        var version = await project.ProjectVersionAsync();
        Assert.Equal("members", (await facilitator.PutAsync(url, "null", version)).Refused(400, "CHAMP_OBLIGATOIRE").GetProperty("field").GetString());
        Assert.Equal("owner", (await facilitator.PutAsync(url, new object[]
        {
            new { login = Logins.Facilitator, role = "owner", is_sponsor = false },
        }, version)).Refused(400, "MEMBRE_ROLE_INVALIDE").GetProperty("role").GetString());
        Assert.Equal("nina@test.local", (await facilitator.PutAsync(url, new object[]
        {
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = false },
            new { login = "nina@test.local", role = "reader", is_sponsor = false },
        }, version)).Refused(400, "MEMBRE_LOGIN_INVALIDE").GetProperty("login").GetString());
        (await facilitator.PutAsync(url, new object[]
        {
            new { login = Logins.Contributor, role = "contributor", is_sponsor = false },
            new { login = NeverInvited, role = "reader", is_sponsor = false },
        }, version)).Refused(400, "PROJET_SANS_ANIMATEUR");

        var participants = (await facilitator.GetAsync(Participants(project))).Ok();
        Assert.Equal([Logins.Facilitator, Logins.Admin, Logins.Contributor, Logins.Reader], LoginsOf(participants));
        var accepted = (await facilitator.PutAsync(url, new object[]
        {
            new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
            new { login = NeverInvited, role = "reader", is_sponsor = false },
        }, version)).Ok();
        Assert.Equal([UserId(accepted.GetProperty("members"), NeverInvited)], Ids(accepted.GetProperty("invited_user_ids")));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_facilitator_who_removes_himself_still_gets_his_success_reply_and_then_loses_the_project(EngineKind kind)
    {
        var project = await ProjectAsync(kind);
        using var facilitator = project.Facilitator;
        using var contributor = project.Contributor;
        var before = (await facilitator.GetAsync(Participants(project))).Ok();
        var replaced = (await facilitator.PutAsync(Members(project), new object[]
        {
            new { login = Logins.Contributor, role = "facilitator", is_sponsor = true },
        }, await project.ProjectVersionAsync())).Ok();
        Assert.Equal([Logins.Contributor], LoginsOf(replaced.GetProperty("members")));
        Assert.Contains(UserId(before, Logins.Facilitator), Ids(replaced.GetProperty("removed_user_ids")));
        (await facilitator.GetAsync(Participants(project))).Refused(404, "INTROUVABLE");
        Assert.Equal("facilitator", (await contributor.GetAsync($"/api/projects/{project.Id}")).Ok().GetProperty("role").GetString());
    }
}
