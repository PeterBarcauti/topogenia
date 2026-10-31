// AssistantFilesTests.cs
// The files joined to the requests of the panel through the whole host on both databases: a Word file read by the host and a PDF sent as it is, the files of a former turn sent again at their place, the bounds of the connector given to the panel, the refusals before anything is sent, the prompt copied, the audit by number and kind (spec 3.0 section 3.4, lot 4).

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Host.Tests.Infrastructure;
using Topogenia.Persistence.Tests.Infrastructure;

namespace Topogenia.Host.Tests.Assistant;

public sealed class AssistantFilesTests(ApiHosts hosts) : IClassFixture<ApiHosts>
{
    private const string Answer = """{"form":"answer","message":"Le document décrit deux étapes.","cited":[],"questions":[],"findings":[],"pmap":null}""";
    private const long Megabyte = 1024 * 1024;

    private static readonly byte[] Pdf = "%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\n%%EOF"u8.ToArray();

    /// <summary>A Word file of two paragraphs, as Office writes its least archive.</summary>
    private static byte[] Docx()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Part(string name, string content)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
            Part("_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>""");
            Part("word/document.xml", """<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Recevoir la demande d'achat</w:t></w:r></w:p><w:p><w:r><w:t>Valider le bon de commande</w:t></w:r></w:p></w:body></w:document>""");
        }
        return stream.ToArray();
    }

    private static object File(string name, byte[] content) => new { name, data = Convert.ToBase64String(content) };

    private static TestConnector Anthropic(ApiHost host) =>
        host.Services.GetServices<IAssistantConnector>().OfType<TestConnector>().Single(item => item.Kind == AssistantConnector.Anthropic);

    private static async Task SetAsync(ApiHost host, string connector)
    {
        using var admin = host.As(Logins.Admin);
        var version = (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("version").GetInt32();
        (await admin.PutAsync("/api/admin/assistant", new
        {
            connector, secret = connector == "anthropic" ? "sk-ant-test-files-0123456789" : null, model = connector == "bedrock" ? "global.anthropic.claude-sonnet-5-5" : null,
            region = connector == "bedrock" ? "eu-west-3" : null, allowed_classifications = new[] { "internal" },
        }, version)).Ok();
    }

    private static JsonElement Last(ApiResponse response)
    {
        Assert.Equal(200, response.Status);
        return JsonDocument.Parse(response.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1]).RootElement.Clone();
    }

    [Theory]
    [MemberData(nameof(Engines.All), MemberType = typeof(Engines))]
    public async Task The_files_go_with_their_request_and_again_with_their_turn_and_are_audited_by_number_and_kind_only(EngineKind kind)
    {
        var host = await hosts.OfAsync(kind);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/assistant/exchanges";

        connector.Next(Answer);
        var first = Last(await facilitator.PostAsync(url, new
        {
            intent = "create", request = "Crée la carte de cette procédure.", selection = Array.Empty<string>(), history = Array.Empty<object>(),
            files = new[] { File("C:\\Mes documents\\procédure.docx", Docx()), File("plan.pdf", Pdf) },
        }));
        Assert.Equal("reply", first.GetProperty("type").GetString());
        var sent = connector.Prompts[^1];
        Assert.Equal(["procédure.docx", "plan.pdf"], sent.Attachments.Select(file => file.Name));
        Assert.Equal("Recevoir la demande d'achat\nValider le bon de commande", sent.Attachments[0].Text);
        Assert.Equal(Pdf, sent.Attachments[1].Content);

        connector.Next(Answer);
        Last(await facilitator.PostAsync(url, new
        {
            intent = "free", request = "Génère quand même.", selection = Array.Empty<string>(),
            history = new[] { new { request = "Crée la carte de cette procédure.", reply = Answer, files = new[] { File("plan.pdf", Pdf) } } },
        }));
        var again = connector.Prompts[^1];
        Assert.Empty(again.Attachments);
        Assert.Equal(["plan.pdf"], again.History[0].Attachments.Select(file => file.Name));

        using var admin = host.As(Logins.Admin);
        var details = (await admin.GetAsync("/api/admin/audit?kind=assistant.exchanged")).Ok().EnumerateArray()
            .Select(audit => audit.GetProperty("details").GetString()!).ToList();
        Assert.Contains(details, detail => detail.Contains("\"files\":2,\"file_kinds\":[\"pdf\",\"word\"]", StringComparison.Ordinal));
        Assert.Contains(details, detail => detail.Contains("\"files\":1,\"file_kinds\":[\"pdf\"]", StringComparison.Ordinal));
        Assert.All(details, detail =>
        {
            Assert.DoesNotContain("procédure", detail, StringComparison.Ordinal);
            Assert.DoesNotContain("Recevoir", detail, StringComparison.Ordinal);
            Assert.DoesNotContain(Convert.ToBase64String(Pdf), detail, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task A_file_refused_is_said_in_clear_before_anything_is_sent()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "anthropic");
        var connector = Anthropic(host);
        var asked = connector.Prompts.Count;
        using var facilitator = project.Facilitator;
        var url = $"/api/maps/{project.RootMapId}/assistant/exchanges";
        object Body(params object[] files) => new { intent = "ask", request = "Que dit ce fichier ?", files };

        var outdated = await facilitator.PostAsync(url, Body(File("ancien.doc", Pdf)));
        Assert.Equal("docx", outdated.Refused(400, AssistantErrors.OutdatedFile).GetProperty("format").GetString());
        Assert.Contains("enregistrez-le au format docx, puis", outdated.Json.GetProperty("message").GetString(), StringComparison.Ordinal);
        (await facilitator.PostAsync(url, Body(File("faux.docx", Pdf)))).Refused(400, AssistantErrors.UnreadableFile);
        (await facilitator.PostAsync(url, Body(File("notes.txt", Pdf)))).Refused(400, AssistantErrors.FileFormat);
        (await facilitator.PostAsync(url, Body(new { name = "plan.pdf", data = "pas du base64 !" }))).Refused(400, "VALEUR_INVALIDE");
        (await facilitator.PostAsync(url, Body(Enumerable.Range(0, 6).Select(index => File($"p{index}.pdf", Pdf)).ToArray()))).Refused(400, AssistantErrors.TooManyFiles);
        Assert.Equal(asked, connector.Prompts.Count);
    }

    [Fact]
    public async Task The_panel_and_the_settings_give_the_bounds_of_the_files_of_the_connector_chosen()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var project = await ApiProject.CreateAsync(host);
        using var reader = project.Reader;
        using var admin = host.As(Logins.Admin);
        await SetAsync(host, "anthropic");
        var files = (await reader.GetAsync($"/api/maps/{project.RootMapId}/assistant")).Ok().GetProperty("files");
        Assert.Equal((5, 10 * Megabyte, 7 * Megabyte, 20 * Megabyte), (files.GetProperty("files").GetInt32(), files.GetProperty("file_bytes").GetInt64(),
            files.GetProperty("image_bytes").GetInt64(), files.GetProperty("total_bytes").GetInt64()));
        Assert.Contains(".vsdx", files.GetProperty("extensions").EnumerateArray().Select(extension => extension.GetString()));
        Assert.Equal("vsdx", files.GetProperty("outdated").GetProperty(".vsd").GetString());
        await SetAsync(host, "bedrock");
        Assert.Equal(4 * Megabyte, (await reader.GetAsync($"/api/maps/{project.RootMapId}/assistant")).Ok().GetProperty("files").GetProperty("file_bytes").GetInt64());
        Assert.Equal(10 * Megabyte, (await admin.GetAsync("/api/admin/assistant")).Ok().GetProperty("files").GetProperty("total_bytes").GetInt64());
    }

    [Fact]
    public async Task The_prompt_copied_holds_the_text_of_a_word_file_and_names_a_pdf_for_the_person_to_join()
    {
        var host = await hosts.OfAsync(EngineKind.Sqlite);
        var project = await ApiProject.CreateAsync(host);
        await SetAsync(host, "copy_paste");
        using var facilitator = project.Facilitator;
        var prompt = (await facilitator.PostAsync($"/api/maps/{project.RootMapId}/assistant/prompt", new
        {
            intent = "create", request = "Crée la carte.", files = new[] { File("procédure.docx", Docx()), File("plan.pdf", Pdf) },
        })).Ok().GetProperty("prompt").GetString()!;
        Assert.Contains("<file name=\"procédure.docx\" kind=\"word\">\nRecevoir la demande d'achat\nValider le bon de commande\n</file>", prompt, StringComparison.Ordinal);
        Assert.Contains("<file name=\"plan.pdf\" kind=\"pdf\">joined by the person to this message</file>", prompt, StringComparison.Ordinal);
    }
}
