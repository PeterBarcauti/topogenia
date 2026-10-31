// HostilePackageTests.cs
// Hostile packages sent to the import routes on both databases (spec 5.4, 7.4, S-11-08, ADR 055): each one refused, nothing written.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Topogenia.Application.Audit;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Exchange;

public sealed class HostilePackageTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private static byte[] Sample => File.ReadAllBytes(RepositoryPaths.Of("samples", "conges.topogenia"));

    /// <summary>The entries of a package by name, in their order, to be changed and written again.</summary>
    private static List<(string Name, byte[] Content)> Entries(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        return [.. archive.Entries.Select(entry =>
        {
            using var stream = entry.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return (entry.FullName, copy.ToArray());
        })];
    }

    private static byte[] Zip(IEnumerable<(string Name, byte[] Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var item = archive.CreateEntry(name).Open();
                item.Write(content);
            }
        }
        return stream.ToArray();
    }

    /// <summary>The sample with one entry changed and announced again in the manifest (size and SHA-256), as the export would write it.</summary>
    private static byte[] Changed(string entry, Action<JsonNode> change, byte[]? from = null)
    {
        var entries = Entries(from ?? Sample);
        var index = entries.FindIndex(item => item.Name == entry);
        var node = JsonNode.Parse(entries[index].Content)!;
        change(node);
        var content = Encoding.UTF8.GetBytes(node.ToJsonString());
        entries[index] = (entry, content);
        return Announced(entries, entry, content);
    }

    private static byte[] Announced(List<(string Name, byte[] Content)> entries, string entry, byte[] content)
    {
        var manifestIndex = entries.FindIndex(item => item.Name == "manifest.json");
        var manifest = JsonNode.Parse(entries[manifestIndex].Content)!;
        var announced = manifest["entries"]!.AsArray().Single(item => (string?)item!["path"] == entry)!;
        announced["size"] = content.LongLength;
        announced["sha256"] = Convert.ToHexStringLower(SHA256.HashData(content));
        entries[manifestIndex] = ("manifest.json", Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        return Zip(entries);
    }

    private static JsonObject Map(JsonNode maps, int index) => maps["maps"]![index]!.AsObject();

    /// <summary>Each hostile package, the code of its refusal and the reason in its details (none for a later format).</summary>
    private static readonly (string Kind, string Code, string Reason)[] Hostiles =
    [
        ("not-a-zip", "PAQUET_INVALIDE", "archive"),
        ("truncated", "PAQUET_INVALIDE", "archive"),
        ("wrong-hash", "PAQUET_INVALIDE", "hash"),
        ("climbing-path", "PAQUET_INVALIDE", "path"),
        ("unknown-entry", "PAQUET_INVALIDE", "entries"),
        ("decompression-bomb", "PAQUET_INVALIDE", "size"),
        ("manifest-too-long", "PAQUET_INVALIDE", "size"),
        ("not-json", "PAQUET_INVALIDE", "json"),
        ("dangling-reference", "PAQUET_INVALIDE", "reference"),
        ("two-steps-in-one-cell", "PAQUET_INVALIDE", "structure"),
        ("output-with-a-wait", "PAQUET_INVALIDE", "structure"),
        ("key-written-twice", "PAQUET_INVALIDE", "json"),
        ("number-beyond-decimal", "PAQUET_INVALIDE", "schema"),
        ("lane-name-of-emojis", "PAQUET_INVALIDE", "values"),
        ("script-in-a-validated-version", "PAQUET_INVALIDE", "values"),
        ("javascript-address", "PAQUET_INVALIDE", "values"),
        ("pain-point-of-two-uses", "PAQUET_INVALIDE", "values"),
        ("translation-of-another-field", "PAQUET_INVALIDE", "values"),
        ("history-comment-too-long", "PAQUET_INVALIDE", "values"),
        ("nul-in-a-validated-version", "PAQUET_INVALIDE", "values"),
        ("login-with-a-space", "PAQUET_INVALIDE", "values"),
        ("raci-role-twice", "PAQUET_INVALIDE", "reference"),
        ("step-of-an-unconfigured-language", "PAQUET_INVALIDE", "values"),
        ("lone-surrogate-in-a-label", "PAQUET_INVALIDE", "json"),
        ("lone-surrogate-in-a-name", "PAQUET_INVALIDE", "json"),
        ("later-format", "PAQUET_TROP_RECENT", ""),
    ];

    private static byte[] Hostile(string kind)
    {
        var entries = Entries(Sample);
        switch (kind)
        {
            case "not-a-zip":
                return Encoding.UTF8.GetBytes("{ \"format\": \"pas un paquet\" }");
            case "truncated":
                return Sample[..(Sample.Length / 2)];
            case "wrong-hash":
                var maps = entries.FindIndex(item => item.Name == "maps.json");
                var bytes = entries[maps].Content.ToArray();
                var at = Array.IndexOf(bytes, (byte)'S');
                bytes[at] = (byte)'T';
                entries[maps] = ("maps.json", bytes);
                return Zip(entries);
            case "climbing-path":
                return Zip([.. entries, ("../../evasion.json", Encoding.UTF8.GetBytes("{}"))]);
            case "unknown-entry":
                return Zip([.. entries.Where(item => item.Name != "members.json"), ("solutions.json", Encoding.UTF8.GetBytes("{}"))]);
            case "decompression-bomb":
                // An entry of 300 MB of zeros, which compresses to almost nothing, read beyond its announced size and the limit.
                var sheets = entries.FindIndex(item => item.Name == "sheets.json");
                entries[sheets] = ("sheets.json", new byte[300 * 1024 * 1024]);
                return Zip(entries);
            case "manifest-too-long":
                var manifest = entries.FindIndex(item => item.Name == "manifest.json");
                entries[manifest] = ("manifest.json", Encoding.UTF8.GetBytes("{\"padding\":\"" + new string('x', 1024 * 1024) + "\"}"));
                return Zip(entries);
            case "not-json":
                var project = entries.FindIndex(item => item.Name == "project.json");
                var garbage = Encoding.UTF8.GetBytes("{ \"project\": ");
                entries[project] = ("project.json", garbage);
                return Announced(entries, "project.json", garbage);
            case "dangling-reference":
                return Changed("maps.json", node => Map(node, 0)["steps"]![1]!["lane"] = "CONGES-C99");
            case "two-steps-in-one-cell":
                return Changed("maps.json", node =>
                {
                    var steps = Map(node, 0)["steps"]!.AsArray();
                    steps[1]!["lane"] = steps[0]!["lane"]!.DeepClone();
                    steps[1]!["column"] = steps[0]!["column"]!.DeepClone();
                    steps[1]!["row"] = steps[0]!["row"]?.DeepClone();
                });
            case "output-with-a-wait":
                return Changed("documents.json", node =>
                    node["documents"]!.AsArray().First(document => (string?)document!["direction"] == "output")!["waitHours"] = 3);
            default:
                return Malformed(kind, entries);
        }
    }

    /// <summary>The packages of the round 1 of the gate P5 (ADR 052 section 3, point 9), and the later format.</summary>
    private static byte[] Malformed(string kind, List<(string Name, byte[] Content)> entries)
    {
        switch (kind)
        {
            case "key-written-twice":
                var named = entries.FindIndex(item => item.Name == "project.json");
                var twice = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(entries[named].Content).Replace("\"name\":", "\"name\": \"Autre\", \"name\":", StringComparison.Ordinal));
                entries[named] = ("project.json", twice);
                return Announced(entries, "project.json", twice);
            case "number-beyond-decimal":
                return Changed("maps.json", node => Map(node, 0)["lanes"]![0]!["sortOrder"] = JsonNode.Parse("1e30"));
            case "lane-name-of-emojis":
                // 100 code points that the schema admits (200 at most), 200 UTF-16 units beyond the 150 of the column.
                return Changed("maps.json", node => Map(node, 0)["lanes"]![0]!["name"] = string.Concat(Enumerable.Repeat("\U0001F600", 100)));
            case "script-in-a-validated-version":
                return Changed("sheets.json", node =>
                {
                    var version = node["sheets"]!.AsArray().First(sheet => sheet!["versions"]!.AsArray().Count > 0)!["versions"]![0]!;
                    version["content"] = ((string)version["content"]!).Replace("Ouvrir", "<script>alert(1)</script>", StringComparison.Ordinal);
                });
            case "javascript-address":
                return Changed("documents.json", node => node["documents"]![0]!["location"] = "javascript:alert(1)");
            case "pain-point-of-two-uses":
                return Changed("documents.json", node =>
                {
                    var uses = node["documents"]![0]!["uses"]!.AsArray();
                    uses[0]!["painPoint"] = uses.First(use => use!["painPoint"] is not null)!["painPoint"]!.DeepClone();
                });
            case "translation-of-another-field":
                return Changed("translations.json", node =>
                    node["translations"]!.AsArray().First(translation => (string?)translation!["object"] == "step")!["field"] = "name");
            case "history-comment-too-long":
                return Changed("painpoints.json", node =>
                    node["painPoints"]!.AsArray().First(painPoint => painPoint!["history"]!.AsArray().Count > 0)!["history"]![0]!["comment"] = new string('c', 1001));
            case "nul-in-a-validated-version":
                // A text of the frozen content ends with a NUL character and a space, which the schema of the entry cannot see.
                return Changed("sheets.json", node =>
                {
                    var version = node["sheets"]!.AsArray().First(sheet => sheet!["versions"]!.AsArray().Count > 0)!["versions"]![0]!;
                    version["content"] = ((string)version["content"]!).Replace("Ouvrir", "Ouvrir\\u0000 ", StringComparison.Ordinal);
                });
            case "login-with-a-space":
                return Changed("project.json", node => node["project"]!["createdBy"] = "EXEMPLE\\alice ");
            case "raci-role-twice":
                return Changed("maps.json", node =>
                {
                    var raci = Map(node, 0)["steps"]!.AsArray().First(step => step!["raci"]!.AsArray().Count > 0)!["raci"]!.AsArray();
                    raci.Add(raci[0]!.DeepClone());
                });
            case "step-of-an-unconfigured-language":
                // Two or three letters, as the schema wants, but a language that this installation does not configure (AR-119).
                return Changed("maps.json", node => Map(node, 0)["steps"]![1]!["sourceLanguage"] = "zzz");
            case "lone-surrogate-in-a-label":
                // Half of a surrogate pair, escaped: valid JSON that the parser accepts and that no text can hold.
                var labelled = entries.FindIndex(item => item.Name == "maps.json");
                var broken = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(entries[labelled].Content)
                    .Replace("\"Saisir la demande\"", "\"Saisir la demande\\ud800\"", StringComparison.Ordinal));
                entries[labelled] = ("maps.json", broken);
                return Announced(entries, "maps.json", broken);
            case "lone-surrogate-in-a-name":
                // The same half in the name of a property: the search of the keys written twice reads it while it parses.
                var withName = entries.FindIndex(item => item.Name == "maps.json");
                var badName = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(entries[withName].Content)
                    .Replace("\"code\": \"CONGES-E002\",", "\"code\": \"CONGES-E002\", \"x\\ud800\": 1,", StringComparison.Ordinal));
                entries[withName] = ("maps.json", badName);
                return Announced(entries, "maps.json", badName);
            default:
                var later = entries.FindIndex(item => item.Name == "manifest.json");
                var node = JsonNode.Parse(entries[later].Content)!;
                node["formatVersion"] = "9.0";
                entries[later] = ("manifest.json", Encoding.UTF8.GetBytes(node.ToJsonString()));
                return Zip(entries);
        }
    }

    [Theory]
    [MemberData(nameof(EngineCases))]
    public async Task A_hostile_package_is_refused_by_the_import_and_its_preview_and_writes_nothing(EngineKind kind, string hostile, string code, string reason)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var projects = (await admin.GetAsync("/api/projects")).Ok().GetArrayLength();
        var imports = (await admin.GetAsync($"/api/admin/audit?kind={AuditKinds.ProjectImported}")).Ok().GetArrayLength();
        var package = Hostile(hostile);

        var preview = await admin.SendAsync(HttpMethod.Post, $"/api/projects/import/preview?with_members=true&code={ApiProject.NewCode()}", package, null);
        var import = await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members=true&code={ApiProject.NewCode()}", package, null);

        foreach (var reply in new[] { preview, import })
        {
            var details = reply.Refused(400, code);
            if (reason.Length > 0)
                Assert.Equal(reason, details.GetProperty("reason").GetString());
        }
        Assert.Equal(projects, (await admin.GetAsync("/api/projects")).Ok().GetArrayLength());
        Assert.Equal(imports, (await admin.GetAsync($"/api/admin/audit?kind={AuditKinds.ProjectImported}")).Ok().GetArrayLength());
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_language_of_origin_not_configured_is_named_by_its_field(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);

        var refused = await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members=true&code={ApiProject.NewCode()}",
            Hostile("step-of-an-unconfigured-language"), null);

        Assert.Equal(["step.sourceLanguage"], refused.Refused(400, "PAQUET_INVALIDE").GetProperty("fields").EnumerateArray().Select(field => field.GetString()));
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task A_translation_that_every_entry_ignores_is_left_out_by_the_import_and_the_others_kept(EngineKind kind)
    {
        using var admin = (await hosts.OfAsync(kind)).As(Logins.Admin);
        var sampleTranslations = 0;
        // The project is French: a French name is in its language of origin (A-2.14-05), "zz" is not configured (AR-119).
        // The step E002 is made English: its English label is in its own language, a French one is added and kept.
        var english = Changed("maps.json", node => Map(node, 0)["steps"]!.AsArray().First(step => (string?)step!["code"] == "CONGES-E002")!["sourceLanguage"] = "en");
        var package = Changed("translations.json", node =>
        {
            var translations = node["translations"]!.AsArray();
            sampleTranslations = translations.Count;
            var name = translations.First(translation => (string?)translation!["object"] == "project")!;
            foreach (var language in new[] { "fr", "zz" })
            {
                var copy = name.DeepClone();
                copy["language"] = language;
                copy["text"] = "Libellé caché";
                translations.Add(copy);
            }
            var label = translations.First(translation => (string?)translation!["step"] == "CONGES-E002")!.DeepClone();
            label["language"] = "fr";
            label["text"] = "Saisir la demande en français";
            translations.Add(label);
        }, english);

        var preview = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import/preview?with_members=true&code={ApiProject.NewCode()}", package, null)).Ok();
        var imported = (await admin.SendAsync(HttpMethod.Post, $"/api/projects/import?with_members=true&code={ApiProject.NewCode()}", package, null)).Ok(201);

        var names = (await admin.GetAsync($"/api/projects/{imported.GetProperty("id").GetGuid()}")).Ok().GetProperty("name_translations");
        Assert.Equal(["en"], names.EnumerateObject().Select(item => item.Name));
        Assert.Equal("Leave request", names.GetProperty("en").GetString());
        var map = (await admin.GetAsync($"/api/maps/{imported.GetProperty("root_map_id").GetGuid()}")).Ok().GetProperty("map");
        var step = map.GetProperty("steps").EnumerateArray().Single(item => item.GetProperty("code").GetString()!.EndsWith("-E002", StringComparison.Ordinal));
        Assert.Equal([("fr", "Saisir la demande en français")],
            step.GetProperty("translations").EnumerateObject().Select(item => (item.Name, item.Value.GetString())));
        // Three translations added, three left out (the project in French and in "zz", the step in English): the preview
        // counts what the import writes, the translations of the sample.
        Assert.Equal(sampleTranslations, preview.GetProperty("counts").GetProperty("translations").GetInt32());
    }

    public static TheoryData<EngineKind, string, string, string> EngineCases()
    {
        var cases = new TheoryData<EngineKind, string, string, string>();
        foreach (var kind in new[] { EngineKind.Sqlite, EngineKind.SqlServer })
        {
            foreach (var (hostile, code, reason) in Hostiles)
                cases.Add(kind, hostile, code, reason);
        }
        return cases;
    }
}
