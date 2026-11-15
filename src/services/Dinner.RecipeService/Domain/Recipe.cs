namespace Dinner.RecipeService.Domain;

/// <summary>A household recipe with Markdown as its source of truth and structured planning data.</summary>
public sealed record Recipe(
    Guid Id, Guid HouseholdId, string Title, string Markdown, string[] Tags, string? Cuisine,
    string[] DietaryRestrictions, RecipeStep[] Instructions, RecipeIngredient[] Ingredients,
    int Revision, string Source, DateTimeOffset CreatedAt)
{
    public static Recipe Create(RecipeDraft draft, string source, DateTimeOffset now, Guid? id = null, int revision = 1, DateTimeOffset? createdAt = null) =>
        new(id ?? Guid.NewGuid(), draft.HouseholdId, draft.Title.Trim(), draft.Markdown.Trim(), Normalize(draft.Tags), string.IsNullOrWhiteSpace(draft.Cuisine) ? null : draft.Cuisine.Trim(), Normalize(draft.DietaryRestrictions), draft.Instructions.Select((step, index) => step.Normalize(index + 1)).ToArray(), draft.Ingredients.Select(ingredient => ingredient.Normalize()).ToArray(), revision, source, createdAt ?? now);

    private static string[] Normalize(string[]? values) => values?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
}

