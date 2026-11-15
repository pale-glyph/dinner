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
app.MapGet("/api/v1/accounts/me", async (HttpRequest request, IConfiguration configuration, NpgsqlDataSource db) =>
{
    var expectedKey = configuration["Dinner:InternalApiKey"];
    var providedKey = request.Headers["X-Internal-Api-Key"].FirstOrDefault();
    var clerkUserId = request.Headers["X-Clerk-User-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(expectedKey, providedKey, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(clerkUserId))
        return Results.Unauthorized();

    var account = await GetOrCreateAccount(db, clerkUserId, request.Headers["X-Account-Email"].FirstOrDefault(), request.Headers["X-Account-Name"].FirstOrDefault());
    return Results.Ok(account);
}).WithTags("Accounts");
app.MapGet("/mcp/resources/household/context", async (Guid householdId, NpgsqlDataSource db) => await Read(db, householdId) is { } household ? Results.Ok(new { uri = "household://context", household }) : Results.NotFound());
app.MapGet("/mcp/resources/household/preferences", async (Guid householdId, NpgsqlDataSource db) => await Read(db, householdId) is { } household ? Results.Ok(new { uri = "household://preferences", household.DietaryPreferences, household.DislikedFoods }) : Results.NotFound());
app.MapGet("/mcp/resources/household/dietary-constraints", async (Guid householdId, NpgsqlDataSource db) => await Read(db, householdId) is { } household ? Results.Ok(new { uri = "household://dietary-constraints", household.Allergies }) : Results.NotFound());
app.MapPost("/mcp/tools/get_household_context", async (McpContext input, NpgsqlDataSource db) => await Read(db, input.HouseholdId) is { } household ? Results.Ok(new { structuredContent = household, summary = $"Household {household.Name} serves {household.Servings}." }) : Results.NotFound());
app.MapPost("/mcp/tools/get_household_preferences", async (McpContext input, NpgsqlDataSource db) => await Read(db, input.HouseholdId) is { } household ? Results.Ok(new { structuredContent = new { household.DietaryPreferences, household.Allergies, household.DislikedFoods } }) : Results.NotFound());
app.MapPost("/mcp/tools/update_household_preferences", async (PreferencesInput input, NpgsqlDataSource db) => { var household = await Read(db, input.HouseholdId); if (household is null) return Results.NotFound(); var updated = household with { DietaryPreferences = input.DietaryPreferences ?? household.DietaryPreferences, Allergies = input.Allergies ?? household.Allergies, DislikedFoods = input.DislikedFoods ?? household.DislikedFoods }; await Save(db, updated); return Results.Ok(new { structuredContent = updated }); });
app.Run();

static async Task MigrateAndSeed(NpgsqlDataSource db)
{
    await using var command = db.CreateCommand("""
        CREATE TABLE IF NOT EXISTS households (id uuid PRIMARY KEY, name text NOT NULL, servings integer NOT NULL, dietary_preferences jsonb NOT NULL DEFAULT '[]', allergies jsonb NOT NULL DEFAULT '[]', disliked_foods jsonb NOT NULL DEFAULT '[]', created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now());
        CREATE TABLE IF NOT EXISTS accounts (id uuid PRIMARY KEY, clerk_user_id text NOT NULL UNIQUE, email text NULL, display_name text NULL, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now());
        CREATE TABLE IF NOT EXISTS household_members (household_id uuid NOT NULL REFERENCES households(id) ON DELETE CASCADE, account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE, role text NOT NULL CHECK (role IN ('owner', 'member')), created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY (household_id, account_id));
        CREATE INDEX IF NOT EXISTS ix_household_members_account_id ON household_members(account_id);
        """);
    await command.ExecuteNonQueryAsync();
}
static async Task<AccountContext> GetOrCreateAccount(NpgsqlDataSource db, string clerkUserId, string? email, string? displayName)
{
    await using var connection = await db.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    Guid? accountId = null;
    await using (var accountCommand = new NpgsqlCommand("SELECT id FROM accounts WHERE clerk_user_id = $1 FOR UPDATE", connection, transaction))
    {
        accountCommand.Parameters.AddWithValue(clerkUserId);
        var existingId = await accountCommand.ExecuteScalarAsync();
        accountId = existingId is Guid id ? id : null;
    }
    if (accountId is null)
    {
        accountId = Guid.NewGuid();
        var household = new Household(Guid.NewGuid(), $"{(string.IsNullOrWhiteSpace(displayName) ? "My" : displayName.Trim())}'s household", 2, [], [], []);
        await using var insertAccount = new NpgsqlCommand("INSERT INTO accounts (id, clerk_user_id, email, display_name) VALUES ($1, $2, $3, $4)", connection, transaction);
        insertAccount.Parameters.AddWithValue(accountId.Value); insertAccount.Parameters.AddWithValue(clerkUserId); insertAccount.Parameters.AddWithValue((object?)email ?? DBNull.Value); insertAccount.Parameters.AddWithValue((object?)displayName ?? DBNull.Value);
        await insertAccount.ExecuteNonQueryAsync();
        await using var insertHousehold = new NpgsqlCommand("INSERT INTO households (id,name,servings,dietary_preferences,allergies,disliked_foods) VALUES ($1,$2,$3,$4::jsonb,$5::jsonb,$6::jsonb)", connection, transaction);
        insertHousehold.Parameters.AddWithValue(household.Id); insertHousehold.Parameters.AddWithValue(household.Name); insertHousehold.Parameters.AddWithValue(household.Servings); insertHousehold.Parameters.AddWithValue("[]"); insertHousehold.Parameters.AddWithValue("[]"); insertHousehold.Parameters.AddWithValue("[]");
        await insertHousehold.ExecuteNonQueryAsync();
        await using var membership = new NpgsqlCommand("INSERT INTO household_members (household_id, account_id, role) VALUES ($1, $2, 'owner')", connection, transaction);
        membership.Parameters.AddWithValue(household.Id); membership.Parameters.AddWithValue(accountId.Value); await membership.ExecuteNonQueryAsync();
    }
    else
    {
        await using var updateAccount = new NpgsqlCommand("UPDATE accounts SET email = $2, display_name = $3, updated_at = now() WHERE id = $1", connection, transaction);
        updateAccount.Parameters.AddWithValue(accountId.Value); updateAccount.Parameters.AddWithValue((object?)email ?? DBNull.Value); updateAccount.Parameters.AddWithValue((object?)displayName ?? DBNull.Value); await updateAccount.ExecuteNonQueryAsync();
    }
    AccountContext result;
    await using (var householdCommand = new NpgsqlCommand("SELECT h.id, h.name, hm.role FROM household_members hm JOIN households h ON h.id = hm.household_id WHERE hm.account_id = $1 ORDER BY CASE hm.role WHEN 'owner' THEN 0 ELSE 1 END, hm.created_at LIMIT 1", connection, transaction))
    {
        householdCommand.Parameters.AddWithValue(accountId.Value);
        await using var reader = await householdCommand.ExecuteReaderAsync();
        await reader.ReadAsync();
        result = new AccountContext(accountId.Value, clerkUserId, email, displayName, new HouseholdMembership(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
    }

    await transaction.CommitAsync();
    return result;
}
static async Task<List<Household>> ReadAll(NpgsqlDataSource db) { var result = new List<Household>(); await using var command = db.CreateCommand("SELECT id, name, servings, dietary_preferences::text, allergies::text, disliked_foods::text FROM households ORDER BY created_at"); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(Map(reader)); return result; }
static async Task<Household?> Read(NpgsqlDataSource db, Guid id) { await using var command = db.CreateCommand("SELECT id, name, servings, dietary_preferences::text, allergies::text, disliked_foods::text FROM households WHERE id = $1"); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(); return await reader.ReadAsync() ? Map(reader) : null; }
static Household Map(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), ReadArray(reader.GetString(3)), ReadArray(reader.GetString(4)), ReadArray(reader.GetString(5)));
static async Task Save(NpgsqlDataSource db, Household household) { await using var command = db.CreateCommand("INSERT INTO households (id,name,servings,dietary_preferences,allergies,disliked_foods) VALUES ($1,$2,$3,$4::jsonb,$5::jsonb,$6::jsonb) ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name, servings=EXCLUDED.servings, dietary_preferences=EXCLUDED.dietary_preferences, allergies=EXCLUDED.allergies, disliked_foods=EXCLUDED.disliked_foods, updated_at=now()"); command.Parameters.AddWithValue(household.Id); command.Parameters.AddWithValue(household.Name); command.Parameters.AddWithValue(household.Servings); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DietaryPreferences)); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.Allergies)); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DislikedFoods)); await command.ExecuteNonQueryAsync(); }
static string[] ReadArray(string json) => System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
record Household(Guid Id, string Name, int Servings, string[] DietaryPreferences, string[] Allergies, string[] DislikedFoods);
record HouseholdInput(string? Name, int? Servings, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods);
record McpContext(Guid HouseholdId, string? CorrelationId);
record PreferencesInput(Guid HouseholdId, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods, string? CorrelationId);
record HouseholdMembership(Guid Id, string Name, string Role);
record AccountContext(Guid Id, string ClerkUserId, string? Email, string? DisplayName, HouseholdMembership Household);
