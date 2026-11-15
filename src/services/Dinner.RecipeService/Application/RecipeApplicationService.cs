using Dinner.RecipeService.Application.Commands;
using Dinner.RecipeService.Application.Ports;
using Dinner.RecipeService.Domain;

namespace Dinner.RecipeService.Application;

public sealed record RecipeResult(
    Recipe? Recipe = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    int? CurrentRevision = null);

public sealed class RecipeApplicationService(IRecipeRepository repository, IClock clock)
{
    public async Task<IReadOnlyList<Recipe>> SearchAsync(RecipeSearchQuery query, CancellationToken cancellationToken) =>
        (await repository.ListAsync(query.HouseholdId, cancellationToken))
        .Where(recipe =>
            (string.IsNullOrWhiteSpace(query.Ingredient) || recipe.Ingredients.Any(ingredient => ingredient.Name.Contains(query.Ingredient, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(query.Cuisine) || string.Equals(recipe.Cuisine, query.Cuisine, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(query.DietaryRestriction) || recipe.DietaryRestrictions.Any(restriction => restriction.Equals(query.DietaryRestriction, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(query.Tag) || recipe.Tags.Any(tag => tag.Equals(query.Tag, StringComparison.OrdinalIgnoreCase))))
        .ToArray();

    public Task<Recipe?> GetAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken) => repository.GetAsync(recipeId, householdId, cancellationToken);

    public async Task<RecipeResult> CreateAsync(CreateRecipeCommand command, string source, CancellationToken cancellationToken)
    {
        var errors = RecipeValidation.Validate(command.Recipe);
        if (errors.Count > 0) return new(Errors: errors);
        var recipe = Recipe.Create(command.Recipe, source, clock.UtcNow);
        await repository.SaveAsync(recipe, cancellationToken);
        return new(recipe);
    }

    public async Task<RecipeResult> UpdateAsync(UpdateRecipeCommand command, string source, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(command.RecipeId, command.Recipe.HouseholdId, cancellationToken);
        if (existing is null) return new();
        if (existing.Revision != command.ExpectedRevision) return new(CurrentRevision: existing.Revision);
        var errors = RecipeValidation.Validate(command.Recipe);
        if (errors.Count > 0) return new(Errors: errors);
        var recipe = Recipe.Create(command.Recipe, source, clock.UtcNow, existing.Id, existing.Revision + 1, existing.CreatedAt);
        await repository.SaveAsync(recipe, cancellationToken);
        return new(recipe);
    }

    public async Task<RecipeResult> DeleteAsync(DeleteRecipeCommand command, CancellationToken cancellationToken)
    {
        var recipe = await repository.GetAsync(command.RecipeId, command.HouseholdId, cancellationToken);
        if (recipe is null) return new();
        if (recipe.Revision != command.ExpectedRevision) return new(CurrentRevision: recipe.Revision);
        await repository.DeleteAsync(recipe, cancellationToken);
        return new(recipe);
    }
}
