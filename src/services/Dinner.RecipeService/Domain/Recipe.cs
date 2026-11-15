namespace Dinner.RecipeService.Domain;

public sealed record Recipe(
    Guid Id,
    Guid HouseholdId,
    string Title,
    string? Description,
    string Instructions,
    int TotalMinutes,
    int Servings,
    string[] Tags,
    RecipeIngredient[] Ingredients,
    int Revision,
    string Source,
    string? ApprovalReference,
    DateTimeOffset CreatedAt)
{
    public static Recipe Create(RecipeDraft draft, string source, DateTimeOffset now, Guid? id = null, int revision = 1, DateTimeOffset? createdAt = null, string? approvalReference = null) =>
        new(id ?? Guid.NewGuid(), draft.HouseholdId, draft.Title.Trim(), draft.Description?.Trim(), draft.Instructions.Trim(), draft.TotalMinutes, draft.Servings, draft.Tags ?? [], draft.Ingredients, revision, source, approvalReference, createdAt ?? now);
}

public sealed record RecipeProposal(
    Guid Id,
    Guid HouseholdId,
    string Kind,
    Guid? RecipeId,
    int? ExpectedRevision,
    RecipeDraft? Recipe,
    string Status,
    DateTimeOffset CreatedAt);

