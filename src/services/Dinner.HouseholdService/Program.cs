using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var connectionString = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
var app = builder.Build().MapDinnerServiceDefaults("household-service");
app.UseCors();
await MigrateAndSeed(app.Services.GetRequiredService<NpgsqlDataSource>());

var api = app.MapGroup("/api/v1/households").WithTags("Households");
api.MapGet("", async (NpgsqlDataSource db) => Results.Ok(await ReadAll(db)));
api.MapGet("/{id:guid}", async (Guid id, NpgsqlDataSource db) => await Read(db, id) is { } household ? Results.Ok(household) : Results.NotFound());
api.MapPost("", async (HouseholdInput input, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
    var household = new Household(Guid.NewGuid(), input.Name.Trim(), input.Servings is > 0 ? input.Servings.Value : 2, input.DietaryPreferences ?? [], input.Allergies ?? [], input.DislikedFoods ?? []);
    await Save(db, household); return Results.Created($"/api/v1/households/{household.Id}", household);
});
api.MapPut("/{id:guid}", async (Guid id, HouseholdInput input, NpgsqlDataSource db) =>
{
    if (string.IsNullOrWhiteSpace(input.Name) || input.Servings is <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["household"] = ["Name and a positive serving count are required."] });
    if (await Read(db, id) is null) return Results.NotFound();
    var household = new Household(id, input.Name.Trim(), input.Servings!.Value, input.DietaryPreferences ?? [], input.Allergies ?? [], input.DislikedFoods ?? []);
    await Save(db, household); return Results.Ok(household);
});
app.MapGet("/mcp/resources/household/context", async (Guid householdId, NpgsqlDataSource db) => await Read(db, householdId) is { } household ? Results.Ok(new { uri = "household://context", household }) : Results.NotFound());
app.MapGet("/mcp/resources/household/preferences", async (Guid householdId, NpgsqlDataSource db) => await Read(db, householdId) is { } household ? Results.Ok(new { uri = "household://preferences", household.DietaryPreferences, household.DislikedFoods }) : Results.NotFound());
app.MapGet("/mcp/resources/household/dietary-constraints", async (Guid householdId, NpgsqlDataSource db) => await Read(db, householdId) is { } household ? Results.Ok(new { uri = "household://dietary-constraints", household.Allergies }) : Results.NotFound());
app.MapPost("/mcp/tools/get_household_context", async (McpContext input, NpgsqlDataSource db) => await Read(db, input.HouseholdId) is { } household ? Results.Ok(new { structuredContent = household, summary = $"Household {household.Name} serves {household.Servings}." }) : Results.NotFound());
app.MapPost("/mcp/tools/get_household_preferences", async (McpContext input, NpgsqlDataSource db) => await Read(db, input.HouseholdId) is { } household ? Results.Ok(new { structuredContent = new { household.DietaryPreferences, household.Allergies, household.DislikedFoods } }) : Results.NotFound());
app.MapPost("/mcp/tools/update_household_preferences", async (PreferencesInput input, NpgsqlDataSource db) => { var household = await Read(db, input.HouseholdId); if (household is null) return Results.NotFound(); var updated = household with { DietaryPreferences = input.DietaryPreferences ?? household.DietaryPreferences, Allergies = input.Allergies ?? household.Allergies, DislikedFoods = input.DislikedFoods ?? household.DislikedFoods }; await Save(db, updated); return Results.Ok(new { structuredContent = updated }); });
app.Run();

static async Task MigrateAndSeed(NpgsqlDataSource db) { await using var command = db.CreateCommand("CREATE TABLE IF NOT EXISTS households (id uuid PRIMARY KEY, name text NOT NULL, servings integer NOT NULL, dietary_preferences jsonb NOT NULL DEFAULT '[]', allergies jsonb NOT NULL DEFAULT '[]', disliked_foods jsonb NOT NULL DEFAULT '[]', created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now())"); await command.ExecuteNonQueryAsync(); await using var count = db.CreateCommand("SELECT count(*) FROM households"); if ((long)(await count.ExecuteScalarAsync() ?? 0L) == 0) await Save(db, new Household(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Dinner household", 2, ["vegetarian-friendly"], [], [])); }
static async Task<List<Household>> ReadAll(NpgsqlDataSource db) { var result = new List<Household>(); await using var command = db.CreateCommand("SELECT id, name, servings, dietary_preferences::text, allergies::text, disliked_foods::text FROM households ORDER BY created_at"); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(Map(reader)); return result; }
static async Task<Household?> Read(NpgsqlDataSource db, Guid id) { await using var command = db.CreateCommand("SELECT id, name, servings, dietary_preferences::text, allergies::text, disliked_foods::text FROM households WHERE id = $1"); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(); return await reader.ReadAsync() ? Map(reader) : null; }
static Household Map(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), ReadArray(reader.GetString(3)), ReadArray(reader.GetString(4)), ReadArray(reader.GetString(5)));
static async Task Save(NpgsqlDataSource db, Household household) { await using var command = db.CreateCommand("INSERT INTO households (id,name,servings,dietary_preferences,allergies,disliked_foods) VALUES ($1,$2,$3,$4::jsonb,$5::jsonb,$6::jsonb) ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name, servings=EXCLUDED.servings, dietary_preferences=EXCLUDED.dietary_preferences, allergies=EXCLUDED.allergies, disliked_foods=EXCLUDED.disliked_foods, updated_at=now()"); command.Parameters.AddWithValue(household.Id); command.Parameters.AddWithValue(household.Name); command.Parameters.AddWithValue(household.Servings); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DietaryPreferences)); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.Allergies)); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DislikedFoods)); await command.ExecuteNonQueryAsync(); }
static string[] ReadArray(string json) => System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
record Household(Guid Id, string Name, int Servings, string[] DietaryPreferences, string[] Allergies, string[] DislikedFoods);
record HouseholdInput(string? Name, int? Servings, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods);
record McpContext(Guid HouseholdId, string? CorrelationId);
record PreferencesInput(Guid HouseholdId, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods, string? CorrelationId);
