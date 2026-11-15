// ProjectTranslations.cs
// A whole project translated into a language through the routes of the workbook, for the tests that need a project wholly translated.

using System.Text.Json;

namespace Topogenia.Host.Tests.Infrastructure;

/// <summary>Translates every text of a project as a workbook brought back would: each text takes a translation made from its original.</summary>
public static class ProjectTranslations
{
    /// <summary>The translation these tests give a text: its language, then the text, short enough for the shortest bound (the label of a flow).</summary>
    public static string Of(string language, string original)
    {
        ArgumentNullException.ThrowIfNull(original);
        var text = $"{language} {original}";
        return text.Length <= 60 ? text : text[..60].TrimEnd();
    }

    /// <summary>Every text of the project that lacks a translation into the language is given one; the number of translations written.</summary>
    public static async Task<int> TranslateAllAsync(ApiClient client, Guid projectId, string language)
    {
        ArgumentNullException.ThrowIfNull(client);
        var list = (await client.GetAsync($"/api/projects/{projectId}/translations?scope=missing&languages={language}")).Ok();
        var rows = new List<object>();
        var line = 2;
        foreach (var text in list.GetProperty("texts").EnumerateArray())
        {
            rows.Add(new
            {
                line = line++, type = text.GetProperty("type").GetString(), id = text.GetProperty("id").GetGuid(), original = text.GetProperty("original").GetString(),
                translations = new Dictionary<string, string> { [language] = Of(language, text.GetProperty("original").GetString()!) },
            });
        }
        if (rows.Count == 0)
            return 0;
        var outcome = (await client.PutAsync($"/api/projects/{projectId}/translations", new { scope = "missing", rows })).Ok();
        Assert.Equal(JsonValueKind.Array, outcome.GetProperty("ignored").ValueKind);
        Assert.Empty(outcome.GetProperty("ignored").EnumerateArray());
        return outcome.GetProperty("written").GetInt32();
    }
}
