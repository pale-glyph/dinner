using Dinner.RecipeService.Domain;

namespace Dinner.RecipeService.Application.Commands;

public sealed record CreateRecipeCommand(RecipeDraft Recipe);
public sealed record UpdateRecipeCommand(Guid RecipeId, int ExpectedRevision, RecipeDraft Recipe);
public sealed record DeleteRecipeCommand(Guid HouseholdId, Guid RecipeId, int ExpectedRevision);
public sealed record RecipeSearchQuery(Guid HouseholdId, string? Ingredient, string? Cuisine = null, string? DietaryRestriction = null, string? Tag = null);
