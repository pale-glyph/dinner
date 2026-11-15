using System.Security.Cryptography;
using System.Text.Json;
using Dinner.HouseholdService.Application.Ports;
using Dinner.HouseholdService.Domain;
using Npgsql;

namespace Dinner.HouseholdService.Infrastructure;

public sealed class NpgsqlHouseholdRepository(NpgsqlDataSource dataSource) : IHouseholdRepository, IHouseholdDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            CREATE TABLE IF NOT EXISTS households (id uuid PRIMARY KEY, name text NOT NULL, servings integer NOT NULL, dietary_preferences jsonb NOT NULL DEFAULT '[]', allergies jsonb NOT NULL DEFAULT '[]', disliked_foods jsonb NOT NULL DEFAULT '[]', created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS accounts (id uuid PRIMARY KEY, clerk_user_id text NOT NULL UNIQUE, email text NULL, display_name text NULL, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS household_members (household_id uuid NOT NULL REFERENCES households(id) ON DELETE CASCADE, account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE, role text NOT NULL CHECK (role IN ('owner', 'member')), created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY (household_id, account_id));
            CREATE INDEX IF NOT EXISTS ix_household_members_account_id ON household_members(account_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_household_members_one_owner ON household_members(household_id) WHERE role = 'owner';
            CREATE TABLE IF NOT EXISTS household_invites (token text PRIMARY KEY, household_id uuid NOT NULL REFERENCES households(id) ON DELETE CASCADE, created_by_account_id uuid NOT NULL REFERENCES accounts(id) ON DELETE CASCADE, created_at timestamptz NOT NULL DEFAULT now(), revoked_at timestamptz NULL);
            """);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AccountContext> GetOrCreateAccountAsync(string clerkUserId, string? email, string? displayName, CancellationToken cancellationToken)
    {
        await using var save = dataSource.CreateCommand("""
            INSERT INTO accounts (id, clerk_user_id, email, display_name) VALUES ($1, $2, $3, $4)
            ON CONFLICT (clerk_user_id) DO UPDATE SET email = EXCLUDED.email, display_name = EXCLUDED.display_name, updated_at = now()
            RETURNING id
            """);
        save.Parameters.AddWithValue(Guid.NewGuid()); save.Parameters.AddWithValue(clerkUserId); save.Parameters.AddWithValue((object?)email ?? DBNull.Value); save.Parameters.AddWithValue((object?)displayName ?? DBNull.Value);
        var accountId = (Guid)(await save.ExecuteScalarAsync(cancellationToken))!;
        return new AccountContext(accountId, clerkUserId, email, displayName, await GetMembershipsAsync(accountId, cancellationToken));
    }

    public async Task CreateAsync(Household household, Guid ownerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var insert = HouseholdCommand("INSERT INTO households (id,name,servings,dietary_preferences,allergies,disliked_foods) VALUES ($1,$2,$3,$4::jsonb,$5::jsonb,$6::jsonb)", household, connection, transaction);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await using var member = new NpgsqlCommand("INSERT INTO household_members (household_id,account_id,role) VALUES ($1,$2,'owner')", connection, transaction);
        member.Parameters.AddWithValue(household.Id); member.Parameters.AddWithValue(ownerId);
        await member.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Household?> GetAsync(Guid householdId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT id,name,servings,dietary_preferences::text,allergies::text,disliked_foods::text FROM households WHERE id=$1");
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapHousehold(reader) : null;
    }

    public async Task UpdateAsync(Household household, CancellationToken cancellationToken)
    {
        await using var command = HouseholdCommand("UPDATE households SET name=$2,servings=$3,dietary_preferences=$4::jsonb,allergies=$5::jsonb,disliked_foods=$6::jsonb,updated_at=now() WHERE id=$1", household);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdMembership>> GetMembershipsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var memberships = new List<HouseholdMembership>();
        await using var command = dataSource.CreateCommand("SELECT h.id,h.name,hm.role FROM household_members hm JOIN households h ON h.id=hm.household_id WHERE hm.account_id=$1 ORDER BY CASE hm.role WHEN 'owner' THEN 0 ELSE 1 END,hm.created_at");
        command.Parameters.AddWithValue(accountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) memberships.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        return memberships;
    }

    public async Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var members = new List<HouseholdMember>();
        await using var command = dataSource.CreateCommand("SELECT a.id,a.display_name,a.email,hm.role FROM household_members hm JOIN accounts a ON a.id=hm.account_id WHERE hm.household_id=$1 ORDER BY CASE hm.role WHEN 'owner' THEN 0 ELSE 1 END,hm.created_at");
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) members.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3)));
        return members;
    }

    public async Task<bool> IsOwnerAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT EXISTS(SELECT 1 FROM household_members WHERE household_id=$1 AND account_id=$2 AND role='owner')");
        command.Parameters.AddWithValue(householdId); command.Parameters.AddWithValue(accountId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> RemoveMemberAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM household_members WHERE household_id=$1 AND account_id=$2 AND role='member'");
        command.Parameters.AddWithValue(householdId); command.Parameters.AddWithValue(accountId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<string> CreateInviteAsync(Guid householdId, Guid createdByAccountId, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        await using var command = dataSource.CreateCommand("INSERT INTO household_invites (token,household_id,created_by_account_id) VALUES ($1,$2,$3)");
        command.Parameters.AddWithValue(token); command.Parameters.AddWithValue(householdId); command.Parameters.AddWithValue(createdByAccountId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return token;
    }

    public async Task<HouseholdMembership?> JoinInviteAsync(string token, Guid accountId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT h.id,h.name FROM household_invites i JOIN households h ON h.id=i.household_id WHERE i.token=$1 AND i.revoked_at IS NULL");
        command.Parameters.AddWithValue(token);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var membership = new HouseholdMembership(reader.GetGuid(0), reader.GetString(1), "member");
        await reader.CloseAsync();
        await using var insert = dataSource.CreateCommand("INSERT INTO household_members (household_id,account_id,role) VALUES ($1,$2,'member') ON CONFLICT (household_id,account_id) DO NOTHING");
        insert.Parameters.AddWithValue(membership.Id); insert.Parameters.AddWithValue(accountId);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        return membership;
    }

    private NpgsqlCommand HouseholdCommand(string sql, Household household, NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null)
    {
        var command = connection is null ? dataSource.CreateCommand(sql) : new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue(household.Id); command.Parameters.AddWithValue(household.Name); command.Parameters.AddWithValue(household.Servings);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(household.DietaryPreferences)); command.Parameters.AddWithValue(JsonSerializer.Serialize(household.Allergies)); command.Parameters.AddWithValue(JsonSerializer.Serialize(household.DislikedFoods));
        return command;
    }

    private static Household MapHousehold(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1), reader.GetInt32(2), ReadArray(reader.GetString(3)), ReadArray(reader.GetString(4)), ReadArray(reader.GetString(5)));
    private static string[] ReadArray(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
}
