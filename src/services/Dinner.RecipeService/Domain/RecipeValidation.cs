namespace Dinner.RecipeService.Domain;

public sealed record RecipeDraft(
    Guid HouseholdId,
    string Title,
    string Markdown,
    string[]? Tags,
    string? Cuisine,
    string[]? DietaryRestrictions,
    RecipeStep[] Instructions,
    RecipeIngredient[] Ingredients);

public sealed record RecipeStep(int Order, string Instruction)
{
    public RecipeStep Normalize(int order) => new(order, Instruction.Trim());
}

public sealed record RecipeIngredient(
    string Name,
    decimal Quantity,
    string Unit,
    Guid? PantryItemId = null,
    string? NutritionProductId = null,
    bool Unresolved = false)
{
    public RecipeIngredient Normalize() => new(Name.Trim(), Quantity, Unit.Trim(), PantryItemId, NutritionProductId, Unresolved);
}

public static class RecipeValidation
{
    public static Dictionary<string, string[]> Validate(RecipeDraft recipe)
    {
        var errors = new Dictionary<string, string[]>();
        if (recipe.HouseholdId == Guid.Empty) errors["householdId"] = ["A household is required."];
        if (string.IsNullOrWhiteSpace(recipe.Title)) errors["title"] = ["A title is required."];
        if (string.IsNullOrWhiteSpace(recipe.Markdown)) errors["markdown"] = ["Recipe Markdown is required."];
        if (recipe.Instructions is null || recipe.Instructions.Length == 0 || recipe.Instructions.Any(step => string.IsNullOrWhiteSpace(step.Instruction))) errors["instructions"] = ["At least one instruction step is required."];
        if (recipe.Ingredients is null || recipe.Ingredients.Length == 0 || recipe.Ingredients.Any(ingredient => string.IsNullOrWhiteSpace(ingredient.Name) || ingredient.Quantity <= 0 || string.IsNullOrWhiteSpace(ingredient.Unit))) errors["ingredients"] = ["Every ingredient requires a name, positive quantity, and unit."];
        return errors;
    }
}
