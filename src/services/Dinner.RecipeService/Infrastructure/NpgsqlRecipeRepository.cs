using System.Text.Json;
using Dinner.RecipeService.Application.Ports;
using Dinner.RecipeService.Domain;
using Npgsql;

namespace Dinner.RecipeService.Infrastructure;

public sealed class NpgsqlRecipeRepository(NpgsqlDataSource dataSource) : IRecipeRepository, IRecipeDatabaseInitializer
{
    private const string RecipeColumns = "id,household_id,title,description,instructions,total_minutes,servings,tags::text,ingredients::text,revision,source,approval_reference,created_at";
    private const string ProposalColumns = "id,household_id,kind,recipe_id,expected_revision,payload::text,status,created_at";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS recipes (id uuid PRIMARY KEY, household_id uuid NOT NULL, title text NOT NULL, description text NULL, instructions text NOT NULL, total_minutes int NOT NULL, servings int NOT NULL, tags jsonb NOT NULL, ingredients jsonb NOT NULL, revision int NOT NULL, source text NOT NULL, approval_reference text NULL, created_at timestamptz NOT NULL, deleted_at timestamptz NULL); CREATE TABLE IF NOT EXISTS recipe_revisions (recipe_id uuid NOT NULL, revision int NOT NULL, payload jsonb NOT NULL, source text NOT NULL, approval_reference text NULL, created_at timestamptz NOT NULL, PRIMARY KEY (recipe_id, revision)); CREATE TABLE IF NOT EXISTS recipe_proposals (id uuid PRIMARY KEY, household_id uuid NOT NULL, kind text NOT NULL, recipe_id uuid NULL, expected_revision int NULL, payload jsonb NULL, status text NOT NULL, approval_reference text NULL, created_at timestamptz NOT NULL); CREATE TABLE IF NOT EXISTS recipe_idempotency (idempotency_key text PRIMARY KEY, recipe_id uuid NOT NULL);");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> ListAsync(Guid householdId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"SELECT {RecipeColumns} FROM recipes WHERE household_id=$1 AND deleted_at IS NULL ORDER BY created_at DESC");
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var recipes = new List<Recipe>();
        while (await reader.ReadAsync(cancellationToken)) recipes.Add(MapRecipe(reader));
        return recipes;
    }

    public async Task<Recipe?> GetAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"SELECT {RecipeColumns} FROM recipes WHERE id=$1 AND household_id=$2 AND deleted_at IS NULL");
        command.Parameters.AddWithValue(recipeId);
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRecipe(reader) : null;
    }

    public async Task SaveAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("INSERT INTO recipes (id,household_id,title,description,instructions,total_minutes,servings,tags,ingredients,revision,source,approval_reference,created_at) VALUES ($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb,$10,$11,$12,$13) ON CONFLICT (id) DO UPDATE SET title=EXCLUDED.title,description=EXCLUDED.description,instructions=EXCLUDED.instructions,total_minutes=EXCLUDED.total_minutes,servings=EXCLUDED.servings,tags=EXCLUDED.tags,ingredients=EXCLUDED.ingredients,revision=EXCLUDED.revision,source=EXCLUDED.source,approval_reference=EXCLUDED.approval_reference; INSERT INTO recipe_revisions (recipe_id,revision,payload,source,approval_reference,created_at) VALUES ($1,$10,$14::jsonb,$11,$12,now()) ON CONFLICT DO NOTHING");
        AddRecipeParameters(command, recipe);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("UPDATE recipes SET deleted_at=now(), revision=revision+1 WHERE id=$1");
        command.Parameters.AddWithValue(recipe.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeProposal>> ListPendingProposalsAsync(Guid householdId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"SELECT {ProposalColumns} FROM recipe_proposals WHERE household_id=$1 AND status='Pending' ORDER BY created_at DESC");
        command.Parameters.AddWithValue(householdId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var proposals = new List<RecipeProposal>();
        while (await reader.ReadAsync(cancellationToken)) proposals.Add(MapProposal(reader));
        return proposals;
    }

    public async Task<RecipeProposal?> GetProposalAsync(Guid proposalId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"SELECT {ProposalColumns} FROM recipe_proposals WHERE id=$1");
        command.Parameters.AddWithValue(proposalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapProposal(reader) : null;
    }

    public async Task SaveProposalAsync(RecipeProposal proposal, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("INSERT INTO recipe_proposals (id,household_id,kind,recipe_id,expected_revision,payload,status,created_at) VALUES ($1,$2,$3,$4,$5,$6::jsonb,$7,$8)");
        command.Parameters.AddWithValue(proposal.Id);
        command.Parameters.AddWithValue(proposal.HouseholdId);
        command.Parameters.AddWithValue(proposal.Kind);
        command.Parameters.AddWithValue((object?)proposal.RecipeId ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)proposal.ExpectedRevision ?? DBNull.Value);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(proposal.Recipe));
        command.Parameters.AddWithValue(proposal.Status);
        command.Parameters.AddWithValue(proposal.CreatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Guid?> GetIdempotentRecipeIdAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT recipe_id FROM recipe_idempotency WHERE idempotency_key=$1");
        command.Parameters.AddWithValue(idempotencyKey);
        return await command.ExecuteScalarAsync(cancellationToken) is Guid recipeId ? recipeId : null;
    }

    public async Task MarkProposalAppliedAsync(Guid proposalId, string approvalReference, string idempotencyKey, Guid recipeId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("UPDATE recipe_proposals SET status='Applied', approval_reference=$2 WHERE id=$1; INSERT INTO recipe_idempotency (idempotency_key,recipe_id) VALUES ($3,$4)");
        command.Parameters.AddWithValue(proposalId);
        command.Parameters.AddWithValue(approvalReference);
        command.Parameters.AddWithValue(idempotencyKey);
        command.Parameters.AddWithValue(recipeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddRecipeParameters(NpgsqlCommand command, Recipe recipe)
    {
        command.Parameters.AddWithValue(recipe.Id);
        command.Parameters.AddWithValue(recipe.HouseholdId);
        command.Parameters.AddWithValue(recipe.Title);
        command.Parameters.AddWithValue((object?)recipe.Description ?? DBNull.Value);
        command.Parameters.AddWithValue(recipe.Instructions);
        command.Parameters.AddWithValue(recipe.TotalMinutes);
        command.Parameters.AddWithValue(recipe.Servings);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe.Tags));
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe.Ingredients));
        command.Parameters.AddWithValue(recipe.Revision);
        command.Parameters.AddWithValue(recipe.Source);
        command.Parameters.AddWithValue((object?)recipe.ApprovalReference ?? DBNull.Value);
        command.Parameters.AddWithValue(recipe.CreatedAt);
    }

    private static Recipe MapRecipe(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), reader.GetInt32(5), reader.GetInt32(6), JsonSerializer.Deserialize<string[]>(reader.GetString(7)) ?? [], JsonSerializer.Deserialize<RecipeIngredient[]>(reader.GetString(8)) ?? [], reader.GetInt32(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetFieldValue<DateTimeOffset>(12));

    private static RecipeProposal MapProposal(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : JsonSerializer.Deserialize<RecipeDraft>(reader.GetString(5)), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7));
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
