// JourneyConnector.cs
// The fake connector of the assistant in the journeys: the test of the connection, and exchanges played from the request (answer, questions, findings, proposal, a proposal corrected, a translation, levels, a refusal, a slow reply, the files received), never a real model.

using System.Text.Json;
using System.Text.Json.Nodes;
using Topogenia.Application.Assistant;
using Topogenia.Application.Ports;
using Topogenia.Domain.Values;

namespace Topogenia.EndToEnd;

/// <summary>
/// A connector of the assistant in the journeys, in the place of the real one of its kind (spec 3.0 section 8: no journey
/// reaches a real model): the model <see cref="RefusedModel"/> is refused as a key would be, any other answers at once
/// with the model it was given. An exchange plays the reply that its practice and its request ask, from the map it
/// receives: a question gets an answer that cites the first step; an analysis, a finding on the steps without a sheet; a
/// creation, a proposal of three lanes and six steps (questions when the request asks for them, a `.pmap` to correct
/// first when it says « invalide »); a translation (practice `translate`, ADR 083) gets the map open, every level, its
/// lanes, steps and project translated into its language, a request that says « tradu » the way to the button of the
/// translations; one that says « niveaux » gets the map open in
/// two levels, one that says « un seul niveau » the project brought back to one level from its schema (ADR 082); a
/// request that says « lent » takes three seconds and tells its progress, one that says « refus » is declined.
/// </summary>
public sealed class JourneyConnector(AssistantConnector kind) : IAssistantConnector
{
    /// <summary>The model a journey names to be refused by the provider.</summary>
    public const string RefusedModel = "refused-model";

    /// <summary>The usage told for each exchange: a first call that writes the cache.</summary>
    public static readonly AssistantUsage Usage = new(1200, 0, 24000, 350);

    public AssistantConnector Kind => kind;

    public Task<AssistantTestOutcome> TestAsync(AssistantConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return Task.FromResult(connection.Settings.Model == RefusedModel
            ? AssistantTestOutcome.Failed(AssistantFailures.Refused, 5, "invalid x-api-key")
            : new AssistantTestOutcome(true, connection.Settings.Model, 1250, 21, 4, null, null));
    }

    public async Task<AssistantExchangeOutcome> ExchangeAsync(AssistantConnection connection, AssistantPrompt prompt, IProgress<AssistantProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(progress);
        var model = connection.Settings.Model ?? AssistantRules.DefaultAnthropicModel;
        if (model == RefusedModel)
            return AssistantExchangeOutcome.Failed(AssistantFailures.Refused, 5, null, "invalid x-api-key");
        var request = prompt.Request;
        progress.Report(new AssistantProgress(AssistantPhase.Thinking, "Je relis la carte et la demande.", 0));
        if (request.Contains("lent", StringComparison.OrdinalIgnoreCase))
        {
            for (var second = 1; second <= 3; second++)
            {
                await Task.Delay(1000, cancellationToken);
                progress.Report(new AssistantProgress(second < 3 ? AssistantPhase.Thinking : AssistantPhase.Writing, $"Étape de réflexion {second}.", second * 100));
            }
        }
        // The request of a correction says the application refused the proposal: it is no request to decline.
        var correcting = request.StartsWith("The application refused", StringComparison.Ordinal);
        if (!correcting && request.Contains("refus", StringComparison.OrdinalIgnoreCase))
            return AssistantExchangeOutcome.Failed(AssistantFailures.Declined, 40, Usage, "cyber");
        progress.Report(new AssistantProgress(AssistantPhase.Writing, null, 120));
        var steps = Steps(prompt.OpenMap);
        object reply;
        if (correcting)
            reply = Proposal("Proposition corrigée.", ValidPmap);
        else if (prompt.Intent == AssistantIntent.Translate && prompt.OpenMap is { } open && prompt.Languages?.Target is { } target)
            reply = Proposal($"Le processus traduit ({target}).", Translated(open, target));
        else if (request.Contains("tradu", StringComparison.OrdinalIgnoreCase))
            reply = Envelope("answer", "Le bouton « Traductions » en haut de la fenêtre traduit tout le processus dans une langue de la liste.", []);
        else if (request.Contains("un seul niveau", StringComparison.OrdinalIgnoreCase) && prompt.Schema is { } schema)
            reply = Proposal("Le processus ramené sur un seul niveau : il s'ouvre en nouveau projet.", Flattened(schema));
        else if (request.Contains("niveaux", StringComparison.OrdinalIgnoreCase) && prompt.OpenMap is { } split && steps.Count > 2)
            reply = Proposal("La carte répartie en deux niveaux.", Split(split));
        else if (prompt.Intent == AssistantIntent.Analyze)
            reply = Envelope("findings", "Un constat sur la carte.", [], findings:
            [
                new { kind = "no_sheet", title = "Étapes sans fiche", detail = "Ces étapes n'ont pas de fiche.", objects = steps.Select(step => step.Code).Take(2).ToArray(),
                    suggestion = "Remplir leur fiche." },
            ]);
        else if (prompt.Intent == AssistantIntent.Create || prompt.Scope == AssistantScope.NewProject)
            reply = request.Contains("questions", StringComparison.OrdinalIgnoreCase)
                ? Envelope("questions", "Deux points à préciser.", [], questions:
                [
                    new { question = "Qui valide la demande ?", choices = new[] { "Le responsable", "La direction" } },
                    new { question = "Quel délai de traitement ?", choices = Array.Empty<string>() },
                ])
                : Proposal("Trois couloirs, six étapes.", request.Contains("invalide", StringComparison.OrdinalIgnoreCase) ? InvalidPmap : ValidPmap);
        else if (prompt.Attachments.Count > 0 || prompt.History.Any(turn => turn.Attachments.Count > 0))
            reply = Envelope("answer", Received(prompt), []);
        else
            reply = Envelope("answer", steps.Count == 0 ? "La carte est vide." : $"La carte compte {steps.Count} étapes ; la première est {steps[0].Code}.",
                steps.Select(step => step.Code).Take(1).ToArray());
        return new AssistantExchangeOutcome(true, model, JsonSerializer.Serialize(reply), Usage, 900, null, null);
    }

    /// <summary>The files the model received (lot 4): those of the request with the start of the text read from a document, those of the former turns counted.</summary>
    private static string Received(AssistantPrompt prompt)
    {
        var files = prompt.Attachments.Select(file => file.Text is { } text
            ? $"{file.Name} ({Codes.Of(file.Kind)}, « {Excerpt(text)} »)"
            : $"{file.Name} ({Codes.Of(file.Kind)}, {file.Size} octets)");
        var former = prompt.History.Sum(turn => turn.Attachments.Count);
        return $"Fichiers reçus : {string.Join(" ; ", files)}" + (former > 0 ? $" ; {former} d'un échange précédent." : ".");
    }

    private static string Excerpt(string text)
    {
        var flat = string.Join(' ', text.Split((char[])['\n', '\t'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return flat[..Math.Min(40, flat.Length)];
    }

    private static object Envelope(string form, string message, string[] cited, object[]? questions = null, object[]? findings = null, string? pmap = null) =>
        new { form, message, cited, questions = questions ?? [], findings = findings ?? [], pmap };

    private static object Proposal(string message, string pmap) => Envelope("proposal", message, [], pmap: pmap);

    /// <summary>The steps of the map sent, by their code (the identifiers of the `.pmap` of a project are its codes).</summary>
    private static List<(string Code, string Label)> Steps(string? openMap)
    {
        if (openMap is null)
            return [];
        using var document = JsonDocument.Parse(openMap);
        return document.RootElement.GetProperty("map").TryGetProperty("steps", out var steps)
            ? steps.EnumerateArray().Select(step => (step.GetProperty("id").GetString()!, step.GetProperty("label").GetString()!)).ToList()
            : [];
    }

    /// <summary>The map open as it is, each lane and step given its English translation: « EN » before its text.</summary>
    private static string Translated(string openMap, string language)
    {
        var document = JsonNode.Parse(openMap)!;
        var mark = language.ToUpperInvariant();
        if (document["project"] is JsonObject project && project["name"] is { } name)
            project["translations"] = new JsonObject { [language] = $"{mark} {name.GetValue<string>()}" };
        Translate(document["map"]!, language, mark);
        return document.ToJsonString();
    }

    /// <summary>The lanes and steps of a map and of every level below it, each given the translation « CODE text » into the language.</summary>
    private static void Translate(JsonNode map, string language, string mark)
    {
        foreach (var (list, text) in new[] { ("lanes", "name"), ("steps", "label") })
        {
            foreach (var item in map[list]?.AsArray() ?? [])
            {
                item!["translations"] = new JsonObject { [language] = $"{mark} {item[text]!.GetValue<string>()}" };
                if (item["sub_map"] is { } sub)
                    Translate(sub, language, mark);
            }
        }
    }

    /// <summary>
    /// The map open in two levels (ADR 082): its first step kept, the others under a new step `prep` in its place,
    /// their lanes named as on the map, in a chain.
    /// </summary>
    private static string Split(string openMap)
    {
        var document = JsonNode.Parse(openMap)!;
        var map = document["map"]!;
        var lanes = map["lanes"]!.AsArray().ToDictionary(lane => lane!["id"]!.GetValue<string>(), lane => lane!["name"]!.GetValue<string>());
        var steps = map["steps"]!.AsArray().Select(step => (Id: step!["id"]!.GetValue<string>(), Lane: step["lane"]!.GetValue<string>(),
            Label: step["label"]!.GetValue<string>())).ToList();
        var inside = steps.Skip(1).ToList();
        var subLanes = inside.Select(step => step.Lane).Distinct().Select((lane, index) => (Lane: lane, Id: $"sub{index}")).ToDictionary(pair => pair.Lane, pair => pair.Id);
        map["lanes"] = new JsonArray([.. lanes.Select(lane => (JsonNode)new JsonObject { ["id"] = lane.Key, ["name"] = lane.Value })]);
        map["steps"] = new JsonArray(
            new JsonObject { ["id"] = steps[0].Id, ["lane"] = steps[0].Lane, ["label"] = steps[0].Label, ["column"] = 0 },
            new JsonObject
            {
                ["id"] = "prep", ["lane"] = inside[0].Lane, ["label"] = "Préparer", ["column"] = 1,
                ["sub_map"] = new JsonObject
                {
                    ["lanes"] = new JsonArray([.. subLanes.Select(pair => (JsonNode)new JsonObject { ["id"] = pair.Value, ["name"] = lanes[pair.Key] })]),
                    ["steps"] = new JsonArray([.. inside.Select((step, column) => (JsonNode)new JsonObject
                        { ["id"] = step.Id, ["lane"] = subLanes[step.Lane], ["label"] = step.Label, ["column"] = column })]),
                    ["flows"] = new JsonArray([.. inside.Zip(inside.Skip(1), (source, target) => (JsonNode)new JsonObject { ["source"] = source.Id, ["target"] = target.Id })]),
                },
            });
        map["flows"] = new JsonArray(new JsonObject { ["source"] = steps[0].Id, ["target"] = "prep" });
        return document.ToJsonString();
    }

    /// <summary>
    /// The project brought back to one level (ADR 082), from its schema (`maps.json` of its package): each detailed step
    /// of the root map replaced by the steps of its levels below, all in the first lane of the root map, in a chain.
    /// </summary>
    private static string Flattened(string schema)
    {
        const string heading = "=== maps.json ===\n";
        var start = schema.IndexOf(heading, StringComparison.Ordinal) + heading.Length;
        var end = schema.IndexOf("\n=== ", start, StringComparison.Ordinal);
        using var package = JsonDocument.Parse(schema[start..(end < 0 ? schema.Length : end)]);
        var maps = package.RootElement.GetProperty("maps").EnumerateArray().ToList();
        static string? Parent(JsonElement map) => map.TryGetProperty("parentStep", out var parent) && parent.ValueKind == JsonValueKind.String ? parent.GetString() : null;
        var detailOf = maps.Where(map => Parent(map) is not null).ToDictionary(map => Parent(map)!);
        var root = maps.Single(map => Parent(map) is null);
        var lane = root.GetProperty("lanes")[0];
        IEnumerable<(string Code, string Label)> Leaves(JsonElement map) => map.GetProperty("steps").EnumerateArray().SelectMany(step =>
            detailOf.TryGetValue(step.GetProperty("code").GetString()!, out var detail)
                ? Leaves(detail)
                : [(step.GetProperty("code").GetString()!, step.GetProperty("label").GetString()!)]);
        var steps = Leaves(root).ToList();
        var laneCode = lane.GetProperty("code").GetString()!;
        return JsonSerializer.Serialize(new
        {
            format = "topogenia", version = "1.1", project = new { name = "À plat", language = "fr" },
            map = new
            {
                lanes = new[] { new { id = laneCode, name = lane.GetProperty("name").GetString()! } },
                steps = steps.Select((step, column) => new { id = step.Code, lane = laneCode, label = step.Label, column }).ToArray(),
                flows = steps.Zip(steps.Skip(1), (source, target) => new { source = source.Code, target = target.Code }).ToArray(),
            },
        });
    }

    /// <summary>The process of the screen of reference (spec 3.0 section 8): three lanes, six steps in sequence.</summary>
    public const string ValidPmap = """
        {"format":"topogenia","version":"1.1","project":{"name":"Demande d'achat","language":"fr"},
         "map":{"lanes":[{"id":"dem","name":"Demandeur"},{"id":"res","name":"Responsable"},{"id":"ach","name":"Achats","type":"department"}],
          "steps":[{"id":"s1","lane":"dem","label":"Besoin identifié","type":"start","column":0},
                   {"id":"s2","lane":"dem","label":"Saisir la demande","column":1,"sheet":{"duration_minutes":10}},
                   {"id":"s3","lane":"res","label":"Valider la demande","column":2},
                   {"id":"s4","lane":"ach","label":"Choisir le fournisseur","column":3},
                   {"id":"s5","lane":"ach","label":"Passer la commande","column":4},
                   {"id":"s6","lane":"dem","label":"Commande passée","type":"end","column":5}],
          "flows":[{"source":"s1","target":"s2"},{"source":"s2","target":"s3"},{"source":"s3","target":"s4"},{"source":"s4","target":"s5"},{"source":"s5","target":"s6"}]},
         "uncertainties":["Le seuil de validation n'est pas précisé."]}
        """;

    /// <summary>A proposal that the format refuses: a step in a lane that does not exist.</summary>
    public const string InvalidPmap = """
        {"format":"topogenia","version":"1.1","project":{"name":"Demande d'achat"},
         "map":{"lanes":[{"id":"dem","name":"Demandeur"}],"steps":[{"id":"s1","lane":"inconnu","label":"Saisir la demande"}],"flows":[]}}
        """;
}
