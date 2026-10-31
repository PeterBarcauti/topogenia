// JsonFieldErrorsTests.cs
// A PATCH body whose field has the wrong type is VALEUR_INVALIDE with the field, as the 1.x; a broken body, or a POST or PUT body of the wrong shape, stays REQUETE_INVALIDE (A-1.2-05).

using System.Text.Json;
using Topogenia.Host.Errors;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Errors;

public sealed class JsonFieldErrorsTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_field_of_the_wrong_type_is_refused_with_its_name_and_a_broken_body_without_one(EngineKind kind)
    {
        using var reader = (await hosts.OfAsync(kind)).As(Logins.Reader);
        var me = (await reader.GetAsync("/api/me")).Ok();
        var version = me.GetProperty("version").GetInt32();
        var wrong = (await reader.PatchAsync("/api/me", "{\"first_visit_done\":\"oui\"}", version)).Refused(400, "VALEUR_INVALIDE");
        Assert.Equal("first_visit_done", wrong.GetProperty("field").GetString());
        var broken = (await reader.PatchAsync("/api/me", "{\"first_visit_done\":", version)).Refused(400, "REQUETE_INVALIDE");
        Assert.False(broken.TryGetProperty("field", out _));
        Assert.Equal(version, (await reader.GetAsync("/api/me")).Ok().GetProperty("version").GetInt32());
        var whole = (await reader.PutAsync("/api/me/last-project", "{\"project_id\":12}")).Refused(400, "REQUETE_INVALIDE");
        Assert.False(whole.TryGetProperty("field", out _));
    }

    [Theory]
    [InlineData("$.first_visit_done", "first_visit_done")]
    [InlineData("$.members[2].role", "role")]
    [InlineData("$.operations[0]", "operations")]
    [InlineData("$", null)]
    public void The_field_is_the_last_property_of_the_path(string path, string? field) =>
        Assert.Equal(field, ErrorMiddleware.FieldOf(new JsonException("wrong type", path, null, null)));
}
