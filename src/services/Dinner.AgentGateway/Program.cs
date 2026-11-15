using Dinner.ServiceDefaults;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddHttpClient("agent-platform");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var connection = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
var app = builder.Build().MapDinnerServiceDefaults("agent-gateway");
app.UseCors();
var runs = new ConcurrentDictionary<Guid, AgentRun>();
var seenEvents = new ConcurrentDictionary<string, byte>();
var config = app.Configuration;
await Migrate(app.Services.GetRequiredService<NpgsqlDataSource>());

app.MapPost("/api/v1/agent/runs", async (StartRun input, IHttpClientFactory clients, NpgsqlDataSource db) =>
{
    if (input.HouseholdId == Guid.Empty || string.IsNullOrWhiteSpace(input.Prompt)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["householdId and prompt are required."] });
    var run = new AgentRun(Guid.NewGuid(), input.HouseholdId, "Starting", null, 0, [], DateTimeOffset.UtcNow, input.CorrelationId ?? Guid.NewGuid().ToString());
    runs[run.Id] = run;
    await SaveRun(db, run);
    var baseUrl = config["AgentPlatform:BaseUrl"];
    var projectId = config["AgentPlatform:ProjectId"];
    var agentId = config["AgentPlatform:AgentId"];
    if (!PlatformUri(baseUrl, out var platformUri) || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(agentId))
    {
        runs[run.Id] = run with { Status = "AwaitingPlatformConfiguration", Events = ["Set AgentPlatform__BaseUrl, ProjectId, and AgentId to start a platform thread."] };
        await SaveRun(db, runs[run.Id]);
        return Results.Accepted($"/api/v1/agent/runs/{run.Id}", runs[run.Id]);
    }
    try
    {
        using var response = await clients.CreateClient("agent-platform").PostAsJsonAsync($"{platformUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agents/{agentId}/threads", new { initialPrompt = input.Prompt });
        if (!response.IsSuccessStatusCode)
        {
            runs[run.Id] = run with { Status = "Failed", Events = [$"The agent platform rejected thread creation ({(int)response.StatusCode})."] };
            await SaveRun(db, runs[run.Id]);
            return Results.Json(runs[run.Id], statusCode: StatusCodes.Status502BadGateway);
        }
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var threadId = document.RootElement.GetProperty("id").GetGuid();
        runs[run.Id] = run with { Status = "Running", ThreadId = threadId };
        await SaveRun(db, runs[run.Id]);
        return Results.Accepted($"/api/v1/agent/runs/{run.Id}", runs[run.Id]);
    }
    catch (HttpRequestException)
    {
        runs[run.Id] = run with { Status = "Failed", Events = ["The configured agent platform could not be reached. Check AGENT_PLATFORM_BASE_URL and that the platform is reachable from the agent-gateway container."] };
        await SaveRun(db, runs[run.Id]);
        return Results.Json(runs[run.Id], statusCode: StatusCodes.Status502BadGateway);
    }
}).WithTags("Agent runs");
app.MapGet("/api/v1/agent/runs/{id:guid}", async (Guid id, NpgsqlDataSource db) => runs.TryGetValue(id, out var run) ? Results.Ok(run) : await ReadRun(db, id) is { } stored ? Results.Ok(stored) : Results.NotFound());
app.MapGet("/api/v1/agent/runs", async (Guid householdId, NpgsqlDataSource db) => householdId == Guid.Empty ? Results.ValidationProblem(new Dictionary<string, string[]> { ["householdId"] = ["householdId is required."] }) : Results.Ok(await ReadRuns(db, householdId)));
app.MapDelete("/api/v1/agent/runs/{id:guid}", async (Guid id, IHttpClientFactory clients, NpgsqlDataSource db) =>
{
    if (!runs.TryGetValue(id, out var run)) { run = await ReadRun(db, id); if (run is null) return Results.NotFound(); }
    var baseUrl = config["AgentPlatform:BaseUrl"]; var projectId = config["AgentPlatform:ProjectId"];
    if (run.ThreadId is not null && PlatformUri(baseUrl, out var platformUri) && !string.IsNullOrWhiteSpace(projectId))
    {
        var response = await clients.CreateClient("agent-platform").DeleteAsync($"{platformUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agent-threads/{run.ThreadId}");
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound) return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
    runs.TryRemove(id, out _); await DeleteRun(db, id); return Results.NoContent();
});
app.MapPost("/api/v1/agent/runs/{id:guid}/messages", async (Guid id, RunMessage input, IHttpClientFactory clients, NpgsqlDataSource db) =>
{
    if (!runs.TryGetValue(id, out var run)) { run = await ReadRun(db, id); if (run is null) return Results.NotFound(); runs[id] = run; }
    if (run.ThreadId is null) return Results.Conflict(new { code = "thread_not_started" });
    if (run.HasPendingQueuedMessage && input.Delivery == "Queued") return Results.Conflict(new { code = "queued_message_pending" });
    var baseUrl = config["AgentPlatform:BaseUrl"]; var projectId = config["AgentPlatform:ProjectId"];
    if (!PlatformUri(baseUrl, out var platformUri) || string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(input.Content)) return Results.Problem("Agent Platform must be configured with an absolute HTTP(S) base URL, project ID, and message content.", statusCode: StatusCodes.Status503ServiceUnavailable);
    var delivery = input.Delivery is "Steering" ? "Steering" : "Queued";
    var response = await clients.CreateClient("agent-platform").PostAsJsonAsync($"{platformUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agent-threads/{run.ThreadId}/messages", new { content = input.Content, delivery });
    if (!response.IsSuccessStatusCode) return Results.StatusCode(502);
    runs[id] = run with { HasPendingQueuedMessage = delivery == "Queued", Status = "Running" };
    await SaveRun(db, runs[id]);
    return Results.Accepted();
});
app.MapPost("/api/v1/agent/runs/{id:guid}/reconcile", async (Guid id, IHttpClientFactory clients, NpgsqlDataSource db) =>
{
    if (!runs.TryGetValue(id, out var run)) { run = await ReadRun(db, id); if (run is null) return Results.NotFound(); runs[id] = run; }
    var baseUrl = config["AgentPlatform:BaseUrl"]; var projectId = config["AgentPlatform:ProjectId"];
    if (run.ThreadId is null) return Results.Conflict(new { code = "thread_not_started" });
    if (!PlatformUri(baseUrl, out var platformUri) || string.IsNullOrWhiteSpace(projectId)) return Results.Problem("Agent Platform must be configured with an absolute HTTP(S) base URL and project ID.", statusCode: StatusCodes.Status503ServiceUnavailable);
    var response = await clients.CreateClient("agent-platform").GetAsync($"{platformUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agent-threads/{run.ThreadId}/timeline?afterSequence={run.LastSequence}&limit=100");
    if (!response.IsSuccessStatusCode) return Results.StatusCode(502);
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    foreach (var entry in document.RootElement.GetProperty("items").EnumerateArray()) run = Advance(run, entry);
    runs[id] = run; await SaveRun(db, run); return Results.Ok(run);
});
app.MapPost("/api/v1/agent/webhooks/events", async (HttpRequest request, NpgsqlDataSource db) =>
{
    using var buffer = new MemoryStream(); await request.Body.CopyToAsync(buffer); var rawBody = buffer.ToArray(); var body = Encoding.UTF8.GetString(rawBody);
    var secret = config["AgentPlatform:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(secret) || !request.Headers.TryGetValue("X-Agent-Platform-Signature", out var signature) || !ValidSignature(rawBody, secret, signature!)) return Results.Unauthorized();
    if (!request.Headers.TryGetValue("X-Agent-Platform-Event-Id", out var eventId) || !seenEvents.TryAdd(eventId!, 0)) return Results.Ok();
    if (!await SaveWebhook(db, eventId!, body)) return Results.Ok();
    using var document = JsonDocument.Parse(body); var root = document.RootElement; var data = root.GetProperty("data");
    if (!data.TryGetProperty("threadId", out var thread)) return Results.Ok();
    var runPair = runs.FirstOrDefault(pair => pair.Value.ThreadId?.ToString() == thread.GetString());
    if (runPair.Value is null) { var stored = Guid.TryParse(thread.GetString(), out var threadId) ? await ReadRunByThread(db, threadId) : null; if (stored is null) return Results.Ok(); runPair = new KeyValuePair<Guid, AgentRun>(stored.Id, stored); runs[stored.Id] = stored; }
    runs[runPair.Key] = Advance(runPair.Value, data);
    await SaveRun(db, runs[runPair.Key]);
    return Results.Ok();
}).WithTags("Agent webhooks");
app.Run();

static bool ValidSignature(byte[] body, string secret, string signature)
{
    const string prefix = "sha256="; if (!signature.StartsWith(prefix, StringComparison.Ordinal) || signature.Length != prefix.Length + 64) return false;
    var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
    try { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature[prefix.Length..])); } catch (FormatException) { return false; }
}
static bool PlatformUri(string? value, out Uri uri) => Uri.TryCreate(value, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
static AgentRun Advance(AgentRun run, JsonElement entry)
{
    var sequence = entry.TryGetProperty("sequenceNumber", out var sequenceNode) && sequenceNode.ValueKind == JsonValueKind.Number ? sequenceNode.GetInt32() : 0;
    if (sequence <= run.LastSequence) return run;
    var status = entry.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String && role.GetString() == "Agent" ? "ProposalReady" : run.Status;
    var content = entry.TryGetProperty("content", out var contentNode) && contentNode.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
        ? contentNode.ValueKind == JsonValueKind.String ? contentNode.GetString() : contentNode.GetRawText()
        : null;
    return run with { LastSequence = sequence, Status = status, HasPendingQueuedMessage = false, Events = content is null ? run.Events : [.. run.Events, content] };
}
static async Task Migrate(NpgsqlDataSource db) { await using var c = db.CreateCommand("CREATE TABLE IF NOT EXISTS agent_runs (id uuid PRIMARY KEY, household_id uuid NOT NULL, status text NOT NULL, thread_id uuid NULL, last_sequence int NOT NULL, events jsonb NOT NULL, created_at timestamptz NOT NULL, correlation_id text NOT NULL, pending_queued bool NOT NULL); CREATE TABLE IF NOT EXISTS agent_webhook_events (event_id text PRIMARY KEY, payload text NOT NULL, received_at timestamptz NOT NULL DEFAULT now());"); await c.ExecuteNonQueryAsync(); }
static async Task SaveRun(NpgsqlDataSource db, AgentRun run) { await using var c = db.CreateCommand("INSERT INTO agent_runs (id,household_id,status,thread_id,last_sequence,events,created_at,correlation_id,pending_queued) VALUES ($1,$2,$3,$4,$5,$6::jsonb,$7,$8,$9) ON CONFLICT (id) DO UPDATE SET status=EXCLUDED.status,thread_id=EXCLUDED.thread_id,last_sequence=EXCLUDED.last_sequence,events=EXCLUDED.events,pending_queued=EXCLUDED.pending_queued"); c.Parameters.AddWithValue(run.Id); c.Parameters.AddWithValue(run.HouseholdId); c.Parameters.AddWithValue(run.Status); c.Parameters.AddWithValue((object?)run.ThreadId ?? DBNull.Value); c.Parameters.AddWithValue(run.LastSequence); c.Parameters.AddWithValue(JsonSerializer.Serialize(run.Events)); c.Parameters.AddWithValue(run.CreatedAt); c.Parameters.AddWithValue(run.CorrelationId); c.Parameters.AddWithValue(run.HasPendingQueuedMessage); await c.ExecuteNonQueryAsync(); }
static async Task<bool> SaveWebhook(NpgsqlDataSource db, string eventId, string payload) { await using var c = db.CreateCommand("INSERT INTO agent_webhook_events (event_id,payload) VALUES ($1,$2) ON CONFLICT DO NOTHING"); c.Parameters.AddWithValue(eventId); c.Parameters.AddWithValue(payload); return await c.ExecuteNonQueryAsync() == 1; }
static async Task<AgentRun?> ReadRun(NpgsqlDataSource db, Guid id) { await using var c = db.CreateCommand("SELECT id,household_id,status,thread_id,last_sequence,events::text,created_at,correlation_id,pending_queued FROM agent_runs WHERE id=$1"); c.Parameters.AddWithValue(id); await using var r = await c.ExecuteReaderAsync(); return await r.ReadAsync() ? MapRun(r) : null; }
static async Task<List<AgentRun>> ReadRuns(NpgsqlDataSource db, Guid householdId) { await using var c = db.CreateCommand("SELECT id,household_id,status,thread_id,last_sequence,events::text,created_at,correlation_id,pending_queued FROM agent_runs WHERE household_id=$1 ORDER BY created_at DESC"); c.Parameters.AddWithValue(householdId); await using var r = await c.ExecuteReaderAsync(); var result = new List<AgentRun>(); while (await r.ReadAsync()) result.Add(MapRun(r)); return result; }
static async Task DeleteRun(NpgsqlDataSource db, Guid id) { await using var c = db.CreateCommand("DELETE FROM agent_runs WHERE id=$1"); c.Parameters.AddWithValue(id); await c.ExecuteNonQueryAsync(); }
static async Task<AgentRun?> ReadRunByThread(NpgsqlDataSource db, Guid threadId) { await using var c = db.CreateCommand("SELECT id,household_id,status,thread_id,last_sequence,events::text,created_at,correlation_id,pending_queued FROM agent_runs WHERE thread_id=$1"); c.Parameters.AddWithValue(threadId); await using var r = await c.ExecuteReaderAsync(); return await r.ReadAsync() ? MapRun(r) : null; }
static AgentRun MapRun(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetGuid(3), r.GetInt32(4), JsonSerializer.Deserialize<string[]>(r.GetString(5)) ?? [], r.GetFieldValue<DateTimeOffset>(6), r.GetString(7), r.GetBoolean(8));
record StartRun(Guid HouseholdId, string Prompt, string? CorrelationId);
record RunMessage(string Content, string? Delivery);
record AgentRun(Guid Id, Guid HouseholdId, string Status, Guid? ThreadId, int LastSequence, string[] Events, DateTimeOffset CreatedAt, string CorrelationId, bool HasPendingQueuedMessage = false);
