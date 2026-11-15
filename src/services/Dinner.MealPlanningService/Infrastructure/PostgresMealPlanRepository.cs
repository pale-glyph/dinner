using System.Text.Json;
using Dinner.MealPlanningService.Application.Ports;
using Dinner.MealPlanningService.Domain;
using Npgsql;
namespace Dinner.MealPlanningService.Infrastructure;

public sealed class PostgresMealPlanRepository(NpgsqlDataSource dataSource) : IMealPlanRepository, IMealPlanDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken ct) { await using var command = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS meal_plans (id uuid PRIMARY KEY, household_id uuid NOT NULL, start_date date NOT NULL, status text NOT NULL, version integer NOT NULL, servings integer NOT NULL, assignments jsonb NOT NULL, constraints jsonb NOT NULL, warnings jsonb NOT NULL, created_at timestamptz NOT NULL)"); await command.ExecuteNonQueryAsync(ct); }
    public async Task SaveAsync(MealPlan plan, CancellationToken ct) { await using var command = dataSource.CreateCommand("INSERT INTO meal_plans (id,household_id,start_date,status,version,servings,assignments,constraints,warnings,created_at) VALUES ($1,$2,$3,$4,$5,$6,$7::jsonb,$8::jsonb,$9::jsonb,$10) ON CONFLICT (id) DO UPDATE SET start_date=EXCLUDED.start_date,status=EXCLUDED.status,version=EXCLUDED.version,servings=EXCLUDED.servings,assignments=EXCLUDED.assignments,constraints=EXCLUDED.constraints,warnings=EXCLUDED.warnings"); command.Parameters.AddWithValue(plan.Id); command.Parameters.AddWithValue(plan.HouseholdId); command.Parameters.AddWithValue(plan.StartDate); command.Parameters.AddWithValue(plan.Status); command.Parameters.AddWithValue(plan.Version); command.Parameters.AddWithValue(plan.Servings); command.Parameters.AddWithValue(JsonSerializer.Serialize(plan.Assignments)); command.Parameters.AddWithValue(JsonSerializer.Serialize(plan.Constraints)); command.Parameters.AddWithValue(JsonSerializer.Serialize(plan.Warnings)); command.Parameters.AddWithValue(plan.CreatedAt); await command.ExecuteNonQueryAsync(ct); }
    public async Task<MealPlan?> GetAsync(Guid id, CancellationToken ct) { await using var command = dataSource.CreateCommand("SELECT id,household_id,start_date,status,version,servings,assignments::text,constraints::text,warnings::text,created_at FROM meal_plans WHERE id=$1"); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? Map(reader) : null; }
    public async Task<IReadOnlyList<MealPlan>> ListAsync(Guid householdId, CancellationToken ct) { await using var command = dataSource.CreateCommand("SELECT id,household_id,start_date,status,version,servings,assignments::text,constraints::text,warnings::text,created_at FROM meal_plans WHERE household_id=$1 ORDER BY created_at DESC"); command.Parameters.AddWithValue(householdId); await using var reader = await command.ExecuteReaderAsync(ct); var plans = new List<MealPlan>(); while (await reader.ReadAsync(ct)) plans.Add(Map(reader)); return plans; }
    private static MealPlan Map(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5), JsonSerializer.Deserialize<MealAssignment[]>(reader.GetString(6)) ?? [], JsonSerializer.Deserialize<string[]>(reader.GetString(7)) ?? [], JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? [], reader.GetFieldValue<DateTimeOffset>(9));
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
