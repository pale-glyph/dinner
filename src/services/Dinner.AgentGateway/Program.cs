using Dinner.ServiceDefaults;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddHttpClient("agent-platform");
var app = builder.Build().MapDinnerServiceDefaults("agent-gateway");
var runs = new ConcurrentDictionary<Guid, AgentRun>();
var seenEvents = new ConcurrentDictionary<string, byte>();
var config = app.Configuration;

app.MapPost("/api/v1/agent/runs", async (StartRun input, IHttpClientFactory clients) =>
{
    if (input.HouseholdId == Guid.Empty || string.IsNullOrWhiteSpace(input.Prompt)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["householdId and prompt are required."] });
    var run = new AgentRun(Guid.NewGuid(), input.HouseholdId, "Starting", null, 0, [], DateTimeOffset.UtcNow, input.CorrelationId ?? Guid.NewGuid().ToString());
    runs[run.Id] = run;
    var baseUrl = config["AgentPlatform:BaseUrl"];
    var projectId = config["AgentPlatform:ProjectId"];
    var agentId = config["AgentPlatform:AgentId"];
    if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(agentId))
    {
        runs[run.Id] = run with { Status = "AwaitingPlatformConfiguration", Events = ["Set AgentPlatform__BaseUrl, ProjectId, and AgentId to start a platform thread."] };
        return Results.Accepted($"/api/v1/agent/runs/{run.Id}", runs[run.Id]);
    }
    try
    {
        using var response = await clients.CreateClient("agent-platform").PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/projects/{projectId}/agents/{agentId}/threads", new { initialPrompt = input.Prompt });
        if (!response.IsSuccessStatusCode) { runs[run.Id] = run with { Status = "Failed", Events = [$"Platform rejected thread creation ({(int)response.StatusCode})."] }; return Results.StatusCode(502); }
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var threadId = document.RootElement.GetProperty("id").GetGuid();
        runs[run.Id] = run with { Status = "Running", ThreadId = threadId };
        return Results.Accepted($"/api/v1/agent/runs/{run.Id}", runs[run.Id]);
    }
    catch (HttpRequestException) { runs[run.Id] = run with { Status = "Failed", Events = ["The configured agent platform could not be reached."] }; return Results.StatusCode(502); }
}).WithTags("Agent runs");
app.MapGet("/api/v1/agent/runs/{id:guid}", (Guid id) => runs.TryGetValue(id, out var run) ? Results.Ok(run) : Results.NotFound());
app.MapPost("/api/v1/agent/runs/{id:guid}/messages", async (Guid id, RunMessage input, IHttpClientFactory clients) =>
{
    if (!runs.TryGetValue(id, out var run)) return Results.NotFound();
    if (run.ThreadId is null) return Results.Conflict(new { code = "thread_not_started" });
    if (run.HasPendingQueuedMessage && input.Delivery == "Queued") return Results.Conflict(new { code = "queued_message_pending" });
    var baseUrl = config["AgentPlatform:BaseUrl"]; var projectId = config["AgentPlatform:ProjectId"];
    if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(input.Content)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["Platform configuration and message content are required."] });
    var delivery = input.Delivery is "Steering" ? "Steering" : "Queued";
    var response = await clients.CreateClient("agent-platform").PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/projects/{projectId}/agent-threads/{run.ThreadId}/messages", new { content = input.Content, delivery });
    if (!response.IsSuccessStatusCode) return Results.StatusCode(502);
    runs[id] = run with { HasPendingQueuedMessage = delivery == "Queued", Status = "Running" };
    return Results.Accepted();
});
app.MapPost("/api/v1/agent/webhooks/events", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8); var body = await reader.ReadToEndAsync();
    var secret = config["AgentPlatform:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(secret) || !request.Headers.TryGetValue("X-Agent-Platform-Signature", out var signature) || !ValidSignature(body, secret, signature!)) return Results.Unauthorized();
    if (!request.Headers.TryGetValue("X-Agent-Platform-Event-Id", out var eventId) || !seenEvents.TryAdd(eventId!, 0)) return Results.Ok();
    using var document = JsonDocument.Parse(body); var root = document.RootElement; var data = root.GetProperty("data");
    if (!data.TryGetProperty("threadId", out var thread)) return Results.Ok();
    var runPair = runs.FirstOrDefault(pair => pair.Value.ThreadId?.ToString() == thread.GetString());
    if (runPair.Value is null) return Results.Ok();
    var sequence = data.TryGetProperty("sequenceNumber", out var value) ? value.GetInt32() : 0;
    if (sequence <= runPair.Value.LastSequence) return Results.Ok();
    var status = data.TryGetProperty("role", out var role) && role.GetString() == "Agent" ? "ProposalReady" : runPair.Value.Status;
    var content = data.TryGetProperty("content", out var contentNode) ? contentNode.GetString() : null;
    runs[runPair.Key] = runPair.Value with { LastSequence = sequence, Status = status, HasPendingQueuedMessage = false, Events = content is null ? runPair.Value.Events : [.. runPair.Value.Events, content] };
    return Results.Ok();
}).WithTags("Agent webhooks");
app.Run();

static bool ValidSignature(string body, string secret, string signature)
{
    const string prefix = "sha256="; if (!signature.StartsWith(prefix, StringComparison.Ordinal) || signature.Length != prefix.Length + 64) return false;
    var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
    try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature[prefix.Length..])); } catch (FormatException) { return false; }
}
record StartRun(Guid HouseholdId, string Prompt, string? CorrelationId);
record RunMessage(string Content, string? Delivery);
record AgentRun(Guid Id, Guid HouseholdId, string Status, Guid? ThreadId, int LastSequence, string[] Events, DateTimeOffset CreatedAt, string CorrelationId, bool HasPendingQueuedMessage = false);
