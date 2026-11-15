namespace Dinner.RecipeService.Domain;

public sealed record RecipeDraft(
    Guid HouseholdId,
    string Title,
    string? Description,
    string Instructions,
    int TotalMinutes,
    int Servings,
    string[]? Tags,
    RecipeIngredient[] Ingredients);

public sealed record RecipeIngredient(
    string Name,
    decimal Quantity,
    string Unit,
    string? NutritionProductId,
    bool Unresolved = false);

public static class RecipeValidation
{
    public static Dictionary<string, string[]> Validate(RecipeDraft recipe)
    {
        var errors = new Dictionary<string, string[]>();
        if (recipe.HouseholdId == Guid.Empty) errors["householdId"] = ["A household is required."];
        if (string.IsNullOrWhiteSpace(recipe.Title)) errors["title"] = ["A title is required."];
        if (recipe.Servings <= 0) errors["servings"] = ["Servings must be positive."];
        if (string.IsNullOrWhiteSpace(recipe.Instructions)) errors["instructions"] = ["Instructions are required."];
        if (recipe.Ingredients is null || recipe.Ingredients.Length == 0 || recipe.Ingredients.Any(ingredient => string.IsNullOrWhiteSpace(ingredient.Name) || ingredient.Quantity <= 0 || string.IsNullOrWhiteSpace(ingredient.Unit))) errors["ingredients"] = ["Every ingredient requires a name, positive quantity, and unit."];
        return errors;
    }
}
