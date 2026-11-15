using Dinner.RecipeService.Application.Commands;
using Dinner.RecipeService.Application.Ports;
using Dinner.RecipeService.Domain;

namespace Dinner.RecipeService.Application;

public sealed record RecipeResult(
    Recipe? Recipe = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    int? CurrentRevision = null);

public sealed record ProposalResult(
    RecipeProposal? Proposal = null,
    Recipe? Recipe = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? Conflict = null,
    int? CurrentRevision = null,
    Guid? IdempotentRecipeId = null);

public sealed class RecipeApplicationService(IRecipeRepository repository, IClock clock)
{
    public async Task<IReadOnlyList<Recipe>> SearchAsync(RecipeSearchQuery query, CancellationToken cancellationToken) =>
        (await repository.ListAsync(query.HouseholdId, cancellationToken))
        .Where(recipe =>
            (string.IsNullOrWhiteSpace(query.Ingredient) || recipe.Ingredients.Any(ingredient => ingredient.Name.Contains(query.Ingredient, StringComparison.OrdinalIgnoreCase))) &&
            (!query.MaxMinutes.HasValue || recipe.TotalMinutes <= query.MaxMinutes) &&
            (string.IsNullOrWhiteSpace(query.MealType) || recipe.Tags.Any(tag => tag.Equals(query.MealType, StringComparison.OrdinalIgnoreCase))) &&
            (string.IsNullOrWhiteSpace(query.Tag) || recipe.Tags.Any(tag => tag.Equals(query.Tag, StringComparison.OrdinalIgnoreCase))))
        .ToArray();

    public Task<Recipe?> GetAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken) => repository.GetAsync(recipeId, householdId, cancellationToken);
    public Task<IReadOnlyList<RecipeProposal>> ListProposalsAsync(Guid householdId, CancellationToken cancellationToken) => repository.ListPendingProposalsAsync(householdId, cancellationToken);

    public async Task<RecipeResult> CreateAsync(CreateRecipeCommand command, CancellationToken cancellationToken)
    {
        var errors = RecipeValidation.Validate(command.Recipe);
        if (errors.Count > 0) return new(Errors: errors);
        var recipe = Recipe.Create(command.Recipe, "user", clock.UtcNow);
        await repository.SaveAsync(recipe, cancellationToken);
        return new(recipe);
    }

    public async Task<RecipeResult> UpdateAsync(UpdateRecipeCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(command.RecipeId, command.Recipe.HouseholdId, cancellationToken);
        if (existing is null) return new();
        if (existing.Revision != command.ExpectedRevision) return new(CurrentRevision: existing.Revision);
        var errors = RecipeValidation.Validate(command.Recipe);
        if (errors.Count > 0) return new(Errors: errors);
        var recipe = Recipe.Create(command.Recipe, "user", clock.UtcNow, existing.Id, existing.Revision + 1, existing.CreatedAt);
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

    public async Task<ProposalResult> ProposeAsync(RecipeProposalCommand command, CancellationToken cancellationToken)
    {
        if (command.Kind != "delete")
        {
            var errors = RecipeValidation.Validate(command.Recipe!);
            if (errors.Count > 0) return new(Errors: errors);
        }

        var householdId = command.HouseholdId ?? command.Recipe?.HouseholdId ?? Guid.Empty;
        var proposal = new RecipeProposal(Guid.NewGuid(), householdId, command.Kind, command.RecipeId, command.ExpectedRevision, command.Recipe, "Pending", clock.UtcNow);
        await repository.SaveProposalAsync(proposal, cancellationToken);
        return new(Proposal: proposal);
    }

    public async Task<ProposalResult> ApplyProposalAsync(ApplyRecipeProposalCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ApprovalReference) || string.IsNullOrWhiteSpace(command.IdempotencyKey))
            return new(Errors: new Dictionary<string, string[]> { ["approval"] = ["Approval reference and idempotency key are required."] });

        if (await repository.GetIdempotentRecipeIdAsync(command.IdempotencyKey, cancellationToken) is { } priorId)
            return new(IdempotentRecipeId: priorId);

        var proposal = await repository.GetProposalAsync(command.ProposalId, cancellationToken);
        if (proposal is null || proposal.Status != "Pending") return new(Conflict: "proposal_not_pending");

        Recipe recipe;
        if (proposal.Kind == "create")
        {
            recipe = Recipe.Create(proposal.Recipe!, "agent", clock.UtcNow, approvalReference: command.ApprovalReference);
            await repository.SaveAsync(recipe, cancellationToken);
        }
        else
        {
            var current = await repository.GetAsync(proposal.RecipeId!.Value, proposal.HouseholdId, cancellationToken);
            if (current is null) return new(Conflict: "not_found");
            if (current.Revision != proposal.ExpectedRevision) return new(CurrentRevision: current.Revision);
            if (proposal.Kind == "delete")
            {
                await repository.DeleteAsync(current, cancellationToken);
                recipe = current;
            }
            else
            {
                recipe = Recipe.Create(proposal.Recipe!, "agent", clock.UtcNow, current.Id, current.Revision + 1, current.CreatedAt, command.ApprovalReference);
                await repository.SaveAsync(recipe, cancellationToken);
            }
        }

        await repository.MarkProposalAppliedAsync(proposal.Id, command.ApprovalReference, command.IdempotencyKey, recipe.Id, cancellationToken);
        return new(Recipe: recipe);
    }
}
