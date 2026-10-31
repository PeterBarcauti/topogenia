// ApiProject.cs
// A project of a test created through the API: its code, root map and first lane, and the clients of its members.

using System.Text.Json;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>
/// A new project created by the administrator through `POST /api/projects`, with a facilitator, a contributor and a
/// reader (<see cref="Logins"/>); the administrator who creates it is put first as facilitator (A-2.1), the stranger is
/// no member. Created by another administrator (<see cref="Logins.Owner"/>), the project leaves <see cref="Admin"/> out
/// of its members. Each project has a code of its own, so that the tests of a class share their base without meeting.
/// </summary>
public sealed class ApiProject
{
    private static int counter;

    private ApiProject(ApiHost host, Guid id, string code, Guid rootMapId, Guid firstLaneId)
    {
        Host = host;
        Id = id;
        Code = code;
        RootMapId = rootMapId;
        FirstLaneId = firstLaneId;
    }

    public ApiHost Host { get; }

    public Guid Id { get; }

    public string Code { get; }

    public Guid RootMapId { get; }

    public Guid FirstLaneId { get; }

    public ApiClient Admin => Host.As(Logins.Admin);

    public ApiClient Facilitator => Host.As(Logins.Facilitator);

    public ApiClient Contributor => Host.As(Logins.Contributor);

    public ApiClient Reader => Host.As(Logins.Reader);

    public ApiClient Stranger => Host.As(Logins.Stranger);

    /// <summary>A new code of 4 capital letters, unique in the process (PROJET_CODE_INVALIDE: 3 to 6 letters A to Z).</summary>
    public static string NewCode()
    {
        var number = Interlocked.Increment(ref counter);
        return string.Concat(Enumerable.Range(0, 4).Select(rank => (char)('A' + (number / (int)Math.Pow(26, 3 - rank)) % 26)));
    }

    public static Task<ApiProject> CreateAsync(ApiHost host) => CreateAsync(host, Logins.Admin);

    /// <summary>A new project created by the administrator <paramref name="creator"/>, who becomes its first facilitator.</summary>
    public static async Task<ApiProject> CreateAsync(ApiHost host, string creator)
    {
        ArgumentNullException.ThrowIfNull(host);
        var code = NewCode();
        using var admin = host.As(creator);
        var created = (await admin.PostAsync("/api/projects", new
        {
            code,
            name = $"Projet {code}",
            members = new object[]
            {
                new { login = Logins.Facilitator, role = "facilitator", is_sponsor = true },
                new { login = Logins.Contributor, role = "contributor", is_sponsor = false },
                new { login = Logins.Reader, role = "reader", is_sponsor = false },
            },
        })).Ok(201);
        var id = created.GetProperty("id").GetGuid();
        var rootMap = created.GetProperty("root_map_id").GetGuid();
        var map = (await admin.GetAsync($"/api/maps/{rootMap}")).Ok();
        return new ApiProject(host, id, code, rootMap, map.GetProperty("map").GetProperty("lanes")[0].GetProperty("id").GetGuid());
    }

    /// <summary>The map read by the facilitator.</summary>
    public async Task<JsonElement> MapAsync(Guid? mapId = null)
    {
        using var client = Facilitator;
        return (await client.GetAsync($"/api/maps/{mapId ?? RootMapId}")).Ok().GetProperty("map");
    }
}
