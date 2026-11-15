using System.Text.Json;
using Dinner.AgentGateway.Application;
using Dinner.AgentGateway.Domain;
using Npgsql;

namespace Dinner.AgentGateway.Infrastructure;

public sealed class PostgresAgentRunRepository(NpgsqlDataSource dataSource) : IAgentRunRepository
{
    private const string RunColumns = "id, household_id, status, thread_id, last_sequence, events::text, created_at, correlation_id, pending_queued";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS agent_runs (id uuid PRIMARY KEY, household_id uuid NOT NULL, status text NOT NULL, thread_id uuid NULL, last_sequence int NOT NULL, events jsonb NOT NULL, created_at timestamptz NOT NULL, correlation_id text NOT NULL, pending_queued bool NOT NULL);
            CREATE TABLE IF NOT EXISTS agent_webhook_events (event_id text PRIMARY KEY, payload text NOT NULL, received_at timestamptz NOT NULL DEFAULT now());
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<AgentRun?> GetAsync(Guid id, CancellationToken cancellationToken = default) => ReadSingleAsync($"SELECT {RunColumns} FROM agent_runs WHERE id = $1", id, cancellationToken);
    public Task<AgentRun?> GetByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default) => ReadSingleAsync($"SELECT {RunColumns} FROM agent_runs WHERE thread_id = $1", threadId, cancellationToken);

    public async Task<IReadOnlyList<AgentRun>> ListAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand($"SELECT {RunColumns} FROM agent_runs WHERE household_id = $1 ORDER BY created_at DESC");
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var runs = new List<AgentRun>();
        while (await reader.ReadAsync(cancellationToken)) runs.Add(Map(reader));
        return runs;
    }

    public async Task SaveAsync(AgentRun run, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO agent_runs (id, household_id, status, thread_id, last_sequence, events, created_at, correlation_id, pending_queued)
            VALUES ($1, $2, $3, $4, $5, $6::jsonb, $7, $8, $9)
            ON CONFLICT (id) DO UPDATE SET status = EXCLUDED.status, thread_id = EXCLUDED.thread_id, last_sequence = EXCLUDED.last_sequence, events = EXCLUDED.events, pending_queued = EXCLUDED.pending_queued;
            """);
        command.Parameters.AddWithValue(run.Id);
        command.Parameters.AddWithValue(run.HouseholdId);
        command.Parameters.AddWithValue(run.Status);
        command.Parameters.AddWithValue((object?)run.ThreadId ?? DBNull.Value);
        command.Parameters.AddWithValue(run.LastSequence);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(run.Events));
        command.Parameters.AddWithValue(run.CreatedAt);
        command.Parameters.AddWithValue(run.CorrelationId);
        command.Parameters.AddWithValue(run.HasPendingQueuedMessage);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM agent_runs WHERE id = $1");
        command.Parameters.AddWithValue(id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<AgentRun?> ReadSingleAsync(string sql, Guid value, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    private static AgentRun Map(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.GetInt32(4), JsonSerializer.Deserialize<string[]>(reader.GetString(5)) ?? [], reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7), reader.GetBoolean(8));
}

public sealed class PostgresWebhookEventRepository(NpgsqlDataSource dataSource) : IWebhookEventRepository
{
    public async Task<bool> TryRecordAsync(string eventId, string payload, CancellationToken cancellationToken = default)
    {
        await using var command = dataSource.CreateCommand("INSERT INTO agent_webhook_events (event_id, payload) VALUES ($1, $2) ON CONFLICT DO NOTHING");
        command.Parameters.AddWithValue(eventId);
        command.Parameters.AddWithValue(payload);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
