// ServerLoad.cs
// The load of NF07 (S-NF-09) on a published host of the server mode: 30 clients at once, each on its project, the usual gestures timed; never shipped; arguments after dotnet run and --: <address> <thumbprint> <clients> <rounds> <report> <prefix of the codes> <pause between two gestures, in ms>.

#:property PublishAot=false

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

var (address, thumbprint) = (new Uri(args[0]), args[1]);
var (clients, rounds, report) = (int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture), int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), args[4]);
var prefix = args.Length > 5 ? args[5] : "CH";
// The time a person takes between two gestures: 0 for a burst, far beyond what 30 people do.
var pause = TimeSpan.FromMilliseconds(args.Length > 6 ? int.Parse(args[6], System.Globalization.CultureInfo.InvariantCulture) : 0);

HttpClient NewClient() => new(new HttpClientHandler
{
    UseDefaultCredentials = true,
    PreAuthenticate = true,
    // The certificate of the run, made for it: accepted by its thumbprint alone.
    ServerCertificateCustomValidationCallback = (_, certificate, _, _) => string.Equals(certificate?.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase),
}) { BaseAddress = address, Timeout = TimeSpan.FromSeconds(60) };

var timings = new System.Collections.Concurrent.ConcurrentBag<(string Phase, string Gesture, double Milliseconds, int Status)>();

async Task<JsonNode?> SendAsync(HttpClient client, string phase, string gesture, HttpMethod method, string path, object? body = null, long? version = null)
{
    using var request = new HttpRequestMessage(method, path);
    if (method != HttpMethod.Get)
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
    if (version is { } value)
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
    if (body is not null)
        request.Content = JsonContent.Create(body);
    if (phase == "together" && pause > TimeSpan.Zero)
        await Task.Delay(pause);
    var watch = Stopwatch.StartNew();
    using var response = await client.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    watch.Stop();
    timings.Add((phase, gesture, watch.Elapsed.TotalMilliseconds, (int)response.StatusCode));
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"{gesture} {method} {path}: {(int)response.StatusCode} {text}");
    return text.Length == 0 ? null : JsonNode.Parse(text);
}

string CodeOf(int index) => prefix + (char)('A' + (index / 26)) + (char)('A' + (index % 26));

// The projects of the clients, made once by the administrator of the run.
using (var setup = NewClient())
{
    for (var index = 0; index < clients; index++)
    {
        await SendAsync(setup, "setup", "create project", HttpMethod.Post, "/api/projects", new
        {
            code = CodeOf(index), name = $"Charge {index + 1}", source_language = "fr", translations = (object?)null, description = (string?)null,
            shared_folder = (string?)null, classification = (string?)null, sharing_group_label = (string?)null, members = Array.Empty<object>(),
        });
    }
}

async Task ClientAsync(int index, string phase)
{
    using var client = NewClient();
    var projects = (await SendAsync(client, phase, "home: projects", HttpMethod.Get, "/api/projects"))!.AsArray();
    var project = projects.First(item => item!["code"]!.GetValue<string>() == CodeOf(index))!;
    var detail = (await SendAsync(client, phase, "project", HttpMethod.Get, $"/api/projects/{project["id"]}"))!;
    var mapId = detail["root_map_id"]!.GetValue<string>();
    for (var round = 0; round < rounds; round++)
    {
        await SendAsync(client, phase, "home: projects", HttpMethod.Get, "/api/projects");
        var map = (await SendAsync(client, phase, "open map", HttpMethod.Get, $"/api/maps/{mapId}"))!["map"]!;
        var lane = map["lanes"]!.AsArray()[0]!["id"]!.GetValue<string>();
        var column = map["steps"]!.AsArray().Count;
        var made = (await SendAsync(client, phase, "add step", HttpMethod.Post, $"/api/maps/{mapId}/steps",
            new { lane_id = lane, column, label = $"Étape {column + 1}", type = "task", row = (int?)null }))!;
        var stepId = made["id"]!.GetValue<string>();
        var read = (await SendAsync(client, phase, "open map", HttpMethod.Get, $"/api/maps/{mapId}"))!["map"]!["steps"]!.AsArray()
            .First(step => step!["id"]!.GetValue<string>() == stepId)!;
        await SendAsync(client, phase, "rename step", HttpMethod.Patch, $"/api/steps/{stepId}", new { label = $"Saisir la demande {column + 1}" },
            read["version"]!.GetValue<long>());
        await SendAsync(client, phase, "read sheet", HttpMethod.Get, $"/api/steps/{stepId}/sheet");
        await SendAsync(client, phase, "save sheet", HttpMethod.Put, $"/api/steps/{stepId}/sheet",
            new { fields = new { }, procedure = new[] { new { text = "Lire la demande", tool = (string?)null, nature = (string?)null } }, free_text = (string?)null }, 0);
    }
}

// A round to warm the host up (its first calls compile), not counted; one client alone, then all of them at once.
await ClientAsync(0, "warm-up");
await ClientAsync(0, "alone");
var total = Stopwatch.StartNew();
await Task.WhenAll(Enumerable.Range(0, clients).Select(index => ClientAsync(index, "together")));
total.Stop();

static double Percentile(List<double> sorted, double share) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(share * sorted.Count) - 1)];
var summary = timings.Where(item => item.Phase is "alone" or "together").GroupBy(item => (item.Phase, item.Gesture)).OrderBy(group => group.Key.Gesture).ThenBy(group => group.Key.Phase)
    .Select(group =>
    {
        var sorted = group.Select(item => item.Milliseconds).Order().ToList();
        return new { phase = group.Key.Phase, gesture = group.Key.Gesture, count = sorted.Count, median = Percentile(sorted, 0.5), p95 = Percentile(sorted, 0.95), max = sorted[^1],
            failures = group.Count(item => item.Status >= 400) };
    }).ToList();
var result = new { address = address.ToString(), clients, rounds, pause_ms = pause.TotalMilliseconds, seconds_together = total.Elapsed.TotalSeconds, requests = timings.Count, summary };
await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
foreach (var line in summary)
    Console.WriteLine($"{line.gesture,-16} {line.phase,-9} n={line.count,4} median={line.median,7:F1} ms p95={line.p95,7:F1} ms max={line.max,7:F1} ms failures={line.failures}");
Console.WriteLine($"together: {clients} clients x {rounds} rounds in {total.Elapsed.TotalSeconds:F1} s, {timings.Count} requests");
