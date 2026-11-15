using System.Text.Json;
using Dinner.RecipeService.Application.Ports;
using Dinner.RecipeService.Domain;
using Npgsql;

namespace Dinner.RecipeService.Infrastructure;

public sealed class NpgsqlRecipeRepository(NpgsqlDataSource dataSource) : IRecipeRepository, IRecipeDatabaseInitializer
{
    private const string RecipeColumns = "id,household_id,title,markdown,tags::text,cuisine,dietary_restrictions::text,instruction_steps::text,ingredients::text,revision,source,created_at";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("CREATE TABLE IF NOT EXISTS recipes (id uuid PRIMARY KEY, household_id uuid NOT NULL, title text NOT NULL, description text NULL, instructions text NOT NULL DEFAULT '', total_minutes int NOT NULL DEFAULT 0, servings int NOT NULL DEFAULT 1, tags jsonb NOT NULL, ingredients jsonb NOT NULL, revision int NOT NULL, source text NOT NULL, approval_reference text NULL, created_at timestamptz NOT NULL, deleted_at timestamptz NULL, markdown text NOT NULL DEFAULT '', cuisine text NULL, dietary_restrictions jsonb NOT NULL DEFAULT '[]'::jsonb, instruction_steps jsonb NOT NULL DEFAULT '[]'::jsonb); CREATE TABLE IF NOT EXISTS recipe_revisions (recipe_id uuid NOT NULL, revision int NOT NULL, payload jsonb NOT NULL, source text NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (recipe_id, revision)); ALTER TABLE recipes ADD COLUMN IF NOT EXISTS markdown text NOT NULL DEFAULT ''; ALTER TABLE recipes ADD COLUMN IF NOT EXISTS cuisine text NULL; ALTER TABLE recipes ADD COLUMN IF NOT EXISTS dietary_restrictions jsonb NOT NULL DEFAULT '[]'::jsonb; ALTER TABLE recipes ADD COLUMN IF NOT EXISTS instruction_steps jsonb NOT NULL DEFAULT '[]'::jsonb; UPDATE recipes SET markdown = '# ' || title || E'\\n\\n' || instructions WHERE markdown = ''; UPDATE recipes SET instruction_steps = jsonb_build_array(jsonb_build_object('order', 1, 'instruction', instructions)) WHERE instruction_steps = '[]'::jsonb AND btrim(instructions) <> '';");
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
        await using var command = dataSource.CreateCommand("INSERT INTO recipes (id,household_id,title,markdown,description,instructions,total_minutes,servings,tags,cuisine,dietary_restrictions,instruction_steps,ingredients,revision,source,approval_reference,created_at) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10,$11::jsonb,$12::jsonb,$13::jsonb,$14,$15,$16,$17) ON CONFLICT (id) DO UPDATE SET title=EXCLUDED.title,markdown=EXCLUDED.markdown,description=EXCLUDED.description,instructions=EXCLUDED.instructions,total_minutes=EXCLUDED.total_minutes,servings=EXCLUDED.servings,tags=EXCLUDED.tags,cuisine=EXCLUDED.cuisine,dietary_restrictions=EXCLUDED.dietary_restrictions,instruction_steps=EXCLUDED.instruction_steps,ingredients=EXCLUDED.ingredients,revision=EXCLUDED.revision,source=EXCLUDED.source,approval_reference=EXCLUDED.approval_reference; INSERT INTO recipe_revisions (recipe_id,revision,payload,source,created_at) VALUES ($1,$14,$18::jsonb,$15,now()) ON CONFLICT DO NOTHING");
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

    private static void AddRecipeParameters(NpgsqlCommand command, Recipe recipe)
    {
        command.Parameters.AddWithValue(recipe.Id);
        command.Parameters.AddWithValue(recipe.HouseholdId);
        command.Parameters.AddWithValue(recipe.Title);
        command.Parameters.AddWithValue(recipe.Markdown);
        command.Parameters.AddWithValue(DBNull.Value);
        command.Parameters.AddWithValue(string.Join("\n", recipe.Instructions.Select(step => step.Instruction)));
        command.Parameters.AddWithValue(0);
        command.Parameters.AddWithValue(1);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe.Tags));
        command.Parameters.AddWithValue((object?)recipe.Cuisine ?? DBNull.Value);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe.DietaryRestrictions));
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe.Instructions));
        command.Parameters.AddWithValue(JsonSerializer.Serialize(recipe.Ingredients));
        command.Parameters.AddWithValue(recipe.Revision);
        command.Parameters.AddWithValue(recipe.Source);
        command.Parameters.AddWithValue(DBNull.Value);
        command.Parameters.AddWithValue(recipe.CreatedAt);
    }

    private static Recipe MapRecipe(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), JsonSerializer.Deserialize<string[]>(reader.GetString(4)) ?? [], reader.IsDBNull(5) ? null : reader.GetString(5), JsonSerializer.Deserialize<string[]>(reader.GetString(6)) ?? [], JsonSerializer.Deserialize<RecipeStep[]>(reader.GetString(7)) ?? [], JsonSerializer.Deserialize<RecipeIngredient[]>(reader.GetString(8)) ?? [], reader.GetInt32(9), reader.GetString(10), reader.GetFieldValue<DateTimeOffset>(11));
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
