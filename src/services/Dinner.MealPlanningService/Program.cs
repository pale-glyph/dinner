using System.Text.Json;
using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var connection = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
var app = builder.Build().MapDinnerServiceDefaults("meal-planning-service");
app.UseCors();
await Migrate(app.Services.GetRequiredService<NpgsqlDataSource>());

var api = app.MapGroup("/api/v1/meal-plans").WithTags("Meal plans");
api.MapGet("", async (Guid householdId, NpgsqlDataSource db) => Results.Ok(await List(db, householdId)));
api.MapGet("/{id:guid}", async (Guid id, NpgsqlDataSource db) => await Read(db, id) is { } plan ? Results.Ok(plan) : Results.NotFound());
api.MapPost("", async (PlanInput input, NpgsqlDataSource db) =>
{
    var errors = Validate(input); if (errors.Count > 0) return Results.ValidationProblem(errors);
    var plan = new MealPlan(Guid.NewGuid(), input.HouseholdId, input.StartDate, "Draft", 1, input.Servings, input.Assignments, input.Constraints ?? [], [], DateTimeOffset.UtcNow);
    await Save(db, plan); return Results.Created($"/api/v1/meal-plans/{plan.Id}", plan);
});
api.MapPut("/{id:guid}", async (Guid id, PlanInput input, int version, NpgsqlDataSource db) =>
{
    var existing = await Read(db, id); if (existing is null) return Results.NotFound();
    if (existing.Status != "Draft" || existing.Version != version) return Results.Conflict(new { code = "version_conflict", detail = "Only the current draft version can be edited." });
    var errors = Validate(input); if (errors.Count > 0) return Results.ValidationProblem(errors);
    var plan = existing with { StartDate = input.StartDate, Servings = input.Servings, Assignments = input.Assignments, Constraints = input.Constraints ?? [], Version = existing.Version + 1, Warnings = [] };
    await Save(db, plan); return Results.Ok(plan);
});
api.MapPost("/{id:guid}/validate", async (Guid id, NpgsqlDataSource db) => await Read(db, id) is { } plan ? Results.Ok(new { planId = id, warnings = BuildWarnings(plan), valid = BuildWarnings(plan).Length == 0 }) : Results.NotFound());
api.MapPost("/{id:guid}/approve", async (Guid id, Approval input, NpgsqlDataSource db) =>
{
    var plan = await Read(db, id); if (plan is null) return Results.NotFound();
    if (plan.Status != "Draft" || plan.Version != input.Version) return Results.Conflict(new { code = "version_conflict" });
    var approved = plan with { Status = "Approved", Version = plan.Version + 1, Warnings = BuildWarnings(plan) };
    await Save(db, approved); return Results.Ok(new { plan = approved, approvalReference = input.ApprovalReference ?? $"meal-plan:{id}:v{approved.Version}" });
});

app.MapGet("/mcp/resources/meal-plans/current", async (Guid householdId, NpgsqlDataSource db) => Results.Ok(new { uri = "meal-plans://current", plans = await List(db, householdId) }));
app.MapPost("/mcp/tools/get_meal_plan", async (PlanLookup input, NpgsqlDataSource db) => await Read(db, input.PlanId) is { } plan && plan.HouseholdId == input.HouseholdId ? Results.Ok(new { structuredContent = plan }) : Results.NotFound());
app.MapPost("/mcp/tools/propose_meal_plan", async (PlanInput input, NpgsqlDataSource db) =>
{
    var errors = Validate(input); if (errors.Count > 0) return Results.ValidationProblem(errors);
    var plan = new MealPlan(Guid.NewGuid(), input.HouseholdId, input.StartDate, "Draft", 1, input.Servings, input.Assignments, input.Constraints ?? [], [], DateTimeOffset.UtcNow);
    plan = plan with { Warnings = BuildWarnings(plan) }; await Save(db, plan);
    return Results.Ok(new { structuredContent = plan, requiresApproval = true, summary = "Meal-plan draft created; approval is required before commitment." });
});
app.MapPost("/mcp/tools/commit_meal_plan", async (CommitPlan input, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(input.ApprovalReference)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["approvalReference"] = ["An approval reference is required."] });
    var plan = await Read(db, input.PlanId); if (plan is null) return Results.NotFound();
    if (plan.Status != "Draft" || plan.Version != input.Version) return Results.Conflict(new { code = "version_conflict" });
    var committed = plan with { Status = "Approved", Version = plan.Version + 1, Warnings = BuildWarnings(plan) }; await Save(db, committed);
    return Results.Ok(new { structuredContent = committed, approvalReference = input.ApprovalReference });
});
app.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "meal-plans://current" }, tools = new[] { "get_meal_plan", "propose_meal_plan", "commit_meal_plan" } }));
app.Run();

static Dictionary<string, string[]> Validate(PlanInput input)
{
    var errors = new Dictionary<string, string[]>();
    if (input.HouseholdId == Guid.Empty) errors["householdId"] = ["A household is required."];
    if (input.Servings <= 0) errors["servings"] = ["Servings must be positive."];
    if (input.Assignments is null || input.Assignments.Length == 0) errors["assignments"] = ["At least one meal assignment is required."];
    else if (input.Assignments.Any(a => string.IsNullOrWhiteSpace(a.RecipeId) || a.Date < input.StartDate || a.Date > input.StartDate.AddDays(6)) || input.Assignments.GroupBy(a => new { a.Date, a.MealType }).Any(g => g.Count() > 1)) errors["assignments"] = ["Assignments must have a recipe, fall within the week, and not duplicate a meal slot."];
    return errors;
}
static string[] BuildWarnings(MealPlan plan) => plan.Assignments.Length < 7 ? ["Fewer than seven meals are planned."] : [];
static async Task Migrate(NpgsqlDataSource db) { await using var c = db.CreateCommand("CREATE TABLE IF NOT EXISTS meal_plans (id uuid PRIMARY KEY, household_id uuid NOT NULL, start_date date NOT NULL, status text NOT NULL, version integer NOT NULL, servings integer NOT NULL, assignments jsonb NOT NULL, constraints jsonb NOT NULL, warnings jsonb NOT NULL, created_at timestamptz NOT NULL)"); await c.ExecuteNonQueryAsync(); }
static async Task Save(NpgsqlDataSource db, MealPlan p) { await using var c = db.CreateCommand("INSERT INTO meal_plans (id,household_id,start_date,status,version,servings,assignments,constraints,warnings,created_at) VALUES ($1,$2,$3,$4,$5,$6,$7::jsonb,$8::jsonb,$9::jsonb,$10) ON CONFLICT (id) DO UPDATE SET start_date=EXCLUDED.start_date,status=EXCLUDED.status,version=EXCLUDED.version,servings=EXCLUDED.servings,assignments=EXCLUDED.assignments,constraints=EXCLUDED.constraints,warnings=EXCLUDED.warnings"); c.Parameters.AddWithValue(p.Id); c.Parameters.AddWithValue(p.HouseholdId); c.Parameters.AddWithValue(p.StartDate); c.Parameters.AddWithValue(p.Status); c.Parameters.AddWithValue(p.Version); c.Parameters.AddWithValue(p.Servings); c.Parameters.AddWithValue(JsonSerializer.Serialize(p.Assignments)); c.Parameters.AddWithValue(JsonSerializer.Serialize(p.Constraints)); c.Parameters.AddWithValue(JsonSerializer.Serialize(p.Warnings)); c.Parameters.AddWithValue(p.CreatedAt); await c.ExecuteNonQueryAsync(); }
static async Task<MealPlan?> Read(NpgsqlDataSource db, Guid id) { await using var c = db.CreateCommand("SELECT id,household_id,start_date,status,version,servings,assignments::text,constraints::text,warnings::text,created_at FROM meal_plans WHERE id=$1"); c.Parameters.AddWithValue(id); await using var r = await c.ExecuteReaderAsync(); return await r.ReadAsync() ? Map(r) : null; }
static async Task<List<MealPlan>> List(NpgsqlDataSource db, Guid householdId) { var list = new List<MealPlan>(); await using var c = db.CreateCommand("SELECT id,household_id,start_date,status,version,servings,assignments::text,constraints::text,warnings::text,created_at FROM meal_plans WHERE household_id=$1 ORDER BY created_at DESC"); c.Parameters.AddWithValue(householdId); await using var r = await c.ExecuteReaderAsync(); while (await r.ReadAsync()) list.Add(Map(r)); return list; }
static MealPlan Map(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetFieldValue<DateOnly>(2), r.GetString(3), r.GetInt32(4), r.GetInt32(5), JsonSerializer.Deserialize<MealAssignment[]>(r.GetString(6)) ?? [], JsonSerializer.Deserialize<string[]>(r.GetString(7)) ?? [], JsonSerializer.Deserialize<string[]>(r.GetString(8)) ?? [], r.GetFieldValue<DateTimeOffset>(9));
record MealPlan(Guid Id, Guid HouseholdId, DateOnly StartDate, string Status, int Version, int Servings, MealAssignment[] Assignments, string[] Constraints, string[] Warnings, DateTimeOffset CreatedAt);
record MealAssignment(DateOnly Date, string MealType, string RecipeId, bool Locked = false);
record PlanInput(Guid HouseholdId, DateOnly StartDate, int Servings, MealAssignment[] Assignments, string[]? Constraints);
record Approval(int Version, string? ApprovalReference);
record PlanLookup(Guid HouseholdId, Guid PlanId);
record CommitPlan(Guid PlanId, int Version, string? ApprovalReference);
