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
api.MapPost("", async (HttpRequest request, HouseholdInput input, NpgsqlDataSource db, IConfiguration configuration) =>
{
    var account = await Authenticate(request, configuration, db); if (account is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(input.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
    var household = new Household(Guid.NewGuid(), input.Name.Trim(), input.Servings is > 0 ? input.Servings.Value : 2, input.DietaryPreferences ?? [], input.Allergies ?? [], input.DislikedFoods ?? []);
    await CreateHousehold(db, household, account.Id); return Results.Created($"/api/v1/households/{household.Id}", new HouseholdMembership(household.Id, household.Name, "owner"));
});
api.MapGet("/mine", async (HttpRequest request, NpgsqlDataSource db, IConfiguration configuration) =>
{
    var account = await Authenticate(request, configuration, db); return account is null ? Results.Unauthorized() : Results.Ok(new { households = await ReadMemberships(db, account.Id) });
});
api.MapPost("/{id:guid}/invites", async (Guid id, HttpRequest request, NpgsqlDataSource db, IConfiguration configuration) =>
{
    var account = await Authenticate(request, configuration, db); if (account is null) return Results.Unauthorized();
    if (!await IsOwner(db, id, account.Id)) return Results.Forbid();
    var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    await using var command = db.CreateCommand("INSERT INTO household_invites (token, household_id, created_by_account_id) VALUES ($1, $2, $3)");
    command.Parameters.AddWithValue(token); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(account.Id); await command.ExecuteNonQueryAsync();
    return Results.Ok(new { token });
});
api.MapPost("/invites/{token}/join", async (string token, HttpRequest request, NpgsqlDataSource db, IConfiguration configuration) =>
{
    var account = await Authenticate(request, configuration, db); if (account is null) return Results.Unauthorized();
    await using var command = db.CreateCommand("SELECT h.id, h.name FROM household_invites i JOIN households h ON h.id = i.household_id WHERE i.token = $1 AND i.revoked_at IS NULL");
    command.Parameters.AddWithValue(token); await using var reader = await command.ExecuteReaderAsync();
    if (!await reader.ReadAsync()) return Results.NotFound(new { error = "This invitation is no longer valid." });
    var household = new HouseholdMembership(reader.GetGuid(0), reader.GetString(1), "member"); await reader.CloseAsync();
    await using var membership = db.CreateCommand("INSERT INTO household_members (household_id, account_id, role) VALUES ($1, $2, 'member') ON CONFLICT (household_id, account_id) DO NOTHING");
    membership.Parameters.AddWithValue(household.Id); membership.Parameters.AddWithValue(account.Id); await membership.ExecuteNonQueryAsync();
    return Results.Ok(household);
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
        CREATE UNIQUE INDEX IF NOT EXISTS ux_household_members_one_owner ON household_members(household_id) WHERE role = 'owner';
        CREATE TABLE IF NOT EXISTS household_invites (token text PRIMARY KEY, household_id uuid NOT NULL REFERENCES households(id) ON DELETE CASCADE, created_by_account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE, created_at timestamptz NOT NULL DEFAULT now(), revoked_at timestamptz NULL);
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
        await using var insertAccount = new NpgsqlCommand("INSERT INTO accounts (id, clerk_user_id, email, display_name) VALUES ($1, $2, $3, $4)", connection, transaction);
        insertAccount.Parameters.AddWithValue(accountId.Value); insertAccount.Parameters.AddWithValue(clerkUserId); insertAccount.Parameters.AddWithValue((object?)email ?? DBNull.Value); insertAccount.Parameters.AddWithValue((object?)displayName ?? DBNull.Value);
        await insertAccount.ExecuteNonQueryAsync();
    }
    else
    {
        await using var updateAccount = new NpgsqlCommand("UPDATE accounts SET email = $2, display_name = $3, updated_at = now() WHERE id = $1", connection, transaction);
        updateAccount.Parameters.AddWithValue(accountId.Value); updateAccount.Parameters.AddWithValue((object?)email ?? DBNull.Value); updateAccount.Parameters.AddWithValue((object?)displayName ?? DBNull.Value); await updateAccount.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
    return new AccountContext(accountId.Value, clerkUserId, email, displayName, await ReadMemberships(db, accountId.Value));
}
static async Task<AccountContext?> Authenticate(HttpRequest request, IConfiguration configuration, NpgsqlDataSource db)
{
    var expectedKey = configuration["Dinner:InternalApiKey"];
    var providedKey = request.Headers["X-Internal-Api-Key"].FirstOrDefault();
    var clerkUserId = request.Headers["X-Clerk-User-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(expectedKey, providedKey, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(clerkUserId)) return null;
    return await GetOrCreateAccount(db, clerkUserId, request.Headers["X-Account-Email"].FirstOrDefault(), request.Headers["X-Account-Name"].FirstOrDefault());
}
static async Task<List<HouseholdMembership>> ReadMemberships(NpgsqlDataSource db, Guid accountId) { var result = new List<HouseholdMembership>(); await using var command = db.CreateCommand("SELECT h.id, h.name, hm.role FROM household_members hm JOIN households h ON h.id = hm.household_id WHERE hm.account_id = $1 ORDER BY CASE hm.role WHEN 'owner' THEN 0 ELSE 1 END, hm.created_at"); command.Parameters.AddWithValue(accountId); await using var reader = await command.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2))); return result; }
static async Task<bool> IsOwner(NpgsqlDataSource db, Guid householdId, Guid accountId) { await using var command = db.CreateCommand("SELECT EXISTS(SELECT 1 FROM household_members WHERE household_id = $1 AND account_id = $2 AND role = 'owner')"); command.Parameters.AddWithValue(householdId); command.Parameters.AddWithValue(accountId); return (bool)(await command.ExecuteScalarAsync())!; }
static async Task CreateHousehold(NpgsqlDataSource db, Household household, Guid ownerId) { await using var connection = await db.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync(); await using var insert = new NpgsqlCommand("INSERT INTO households (id,name,servings,dietary_preferences,allergies,disliked_foods) VALUES ($1,$2,$3,$4::jsonb,$5::jsonb,$6::jsonb)", connection, transaction); insert.Parameters.AddWithValue(household.Id); insert.Parameters.AddWithValue(household.Name); insert.Parameters.AddWithValue(household.Servings); insert.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DietaryPreferences)); insert.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.Allergies)); insert.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DislikedFoods)); await insert.ExecuteNonQueryAsync(); await using var member = new NpgsqlCommand("INSERT INTO household_members (household_id,account_id,role) VALUES ($1,$2,'owner')", connection, transaction); member.Parameters.AddWithValue(household.Id); member.Parameters.AddWithValue(ownerId); await member.ExecuteNonQueryAsync(); await transaction.CommitAsync(); }
static async Task<Household?> Read(NpgsqlDataSource db, Guid id) { await using var command = db.CreateCommand("SELECT id, name, servings, dietary_preferences::text, allergies::text, disliked_foods::text FROM households WHERE id = $1"); command.Parameters.AddWithValue(id); await using var reader = await command.ExecuteReaderAsync(); return await reader.ReadAsync() ? Map(reader) : null; }
static Household Map(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), ReadArray(reader.GetString(3)), ReadArray(reader.GetString(4)), ReadArray(reader.GetString(5)));
static async Task Save(NpgsqlDataSource db, Household household) { await using var command = db.CreateCommand("INSERT INTO households (id,name,servings,dietary_preferences,allergies,disliked_foods) VALUES ($1,$2,$3,$4::jsonb,$5::jsonb,$6::jsonb) ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name, servings=EXCLUDED.servings, dietary_preferences=EXCLUDED.dietary_preferences, allergies=EXCLUDED.allergies, disliked_foods=EXCLUDED.disliked_foods, updated_at=now()"); command.Parameters.AddWithValue(household.Id); command.Parameters.AddWithValue(household.Name); command.Parameters.AddWithValue(household.Servings); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DietaryPreferences)); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.Allergies)); command.Parameters.AddWithValue(System.Text.Json.JsonSerializer.Serialize(household.DislikedFoods)); await command.ExecuteNonQueryAsync(); }
static string[] ReadArray(string json) => System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
record Household(Guid Id, string Name, int Servings, string[] DietaryPreferences, string[] Allergies, string[] DislikedFoods);
record HouseholdInput(string? Name, int? Servings, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods);
record McpContext(Guid HouseholdId, string? CorrelationId);
record PreferencesInput(Guid HouseholdId, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods, string? CorrelationId);
record HouseholdMembership(Guid Id, string Name, string Role);
record AccountContext(Guid Id, string ClerkUserId, string? Email, string? DisplayName, List<HouseholdMembership> Households);
