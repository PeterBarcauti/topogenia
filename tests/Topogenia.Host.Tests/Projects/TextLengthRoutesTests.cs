// TextLengthRoutesTests.cs
// The bounds of the texts of the profile and of a new project through the routes: the last character allowed is kept, one more is TEXTE_TROP_LONG (A-1.2-04, A-1.2-09 to 11).

using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Projects;

/// <summary>The bounds are written here as annex A says them (128, 200, 4000, 400), not read from the code under test.</summary>
public sealed class TextLengthRoutesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_display_name_of_128_characters_is_kept_and_one_of_129_is_refused_too_long_naming_the_field_and_its_bound(EngineKind kind)
    {
        var project = await ApiProject.CreateAsync(await hosts.OfAsync(kind));
        using var reader = project.Reader;
        async Task<int> VersionAsync() => (await reader.GetAsync("/api/me")).Ok().GetProperty("version").GetInt32();
        var longest = new string('é', 128);

        var details = (await reader.PatchAsync("/api/me", new { display_name = longest + "e" }, await VersionAsync())).Refused(400, "TEXTE_TROP_LONG");
        Assert.Equal(("display_name", 128), (details.GetProperty("field").GetString(), details.GetProperty("max").GetInt32()));
        Assert.NotEqual(longest, (await reader.GetAsync("/api/me")).Ok().GetProperty("display_name").GetString());

        Assert.Equal(longest, (await reader.PatchAsync("/api/me", new { display_name = longest }, await VersionAsync())).Ok().GetProperty("display_name").GetString());
        Assert.Equal(longest, (await reader.GetAsync("/api/me")).Ok().GetProperty("display_name").GetString());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_name_the_description_and_the_shared_folder_of_a_new_project_are_bounded_and_nothing_is_created_past_a_bound(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        using var admin = host.As(Logins.Admin);
        var (name, description, folder) = (new string('n', 200), new string('d', 4000), @"\\serveur\partage\" + new string('f', 400 - 18));
        Assert.Equal(400, folder.Length);
        var refusals = new (object Body, string Field, int Max)[]
        {
            (new { code = ApiProject.NewCode(), name = name + "n" }, "name", 200),
            (new { code = ApiProject.NewCode(), name, description = description + "d" }, "description", 4000),
            (new { code = ApiProject.NewCode(), name, shared_folder = folder + "f" }, "shared_folder", 400),
        };
        foreach (var (body, field, max) in refusals)
        {
            var details = (await admin.PostAsync("/api/projects", body)).Refused(400, "TEXTE_TROP_LONG");
            Assert.Equal((field, max), (details.GetProperty("field").GetString(), details.GetProperty("max").GetInt32()));
        }
        var listed = (await admin.GetAsync("/api/projects")).Ok().EnumerateArray().Select(item => item.GetProperty("name").GetString());
        Assert.DoesNotContain(listed, text => text is not null && text.StartsWith("nnn", StringComparison.Ordinal));

        var code = ApiProject.NewCode();
        var created = (await admin.PostAsync("/api/projects", new { code, name, description, shared_folder = folder })).Ok(201);
        var read = (await admin.GetAsync($"/api/projects/{created.GetProperty("id").GetGuid()}")).Ok();
        Assert.Equal((code, name, description, folder),
            (read.GetProperty("code").GetString(), read.GetProperty("name").GetString(), read.GetProperty("description").GetString(), read.GetProperty("shared_folder").GetString()));
    }
}
