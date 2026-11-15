using Dinner.RecipeService.Domain;

namespace Dinner.RecipeService.Application.Commands;

public sealed record CreateRecipeCommand(RecipeDraft Recipe);
public sealed record UpdateRecipeCommand(Guid RecipeId, int ExpectedRevision, RecipeDraft Recipe);
public sealed record DeleteRecipeCommand(Guid HouseholdId, Guid RecipeId, int ExpectedRevision);
public sealed record RecipeSearchQuery(Guid HouseholdId, string? Ingredient, int? MaxMinutes = null, string? MealType = null, string? Tag = null);
public sealed record RecipeProposalCommand(string Kind, Guid? RecipeId, RecipeDraft? Recipe, Guid? HouseholdId = null, int? ExpectedRevision = null);
public sealed record ApplyRecipeProposalCommand(Guid ProposalId, string ApprovalReference, string IdempotencyKey);
