using Dinner.RecipeService.Domain;

namespace Dinner.RecipeService.Application.Ports;

public interface IRecipeDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IRecipeRepository
{
    Task<IReadOnlyList<Recipe>> ListAsync(Guid householdId, CancellationToken cancellationToken);
    Task<Recipe?> GetAsync(Guid recipeId, Guid householdId, CancellationToken cancellationToken);
    Task SaveAsync(Recipe recipe, CancellationToken cancellationToken);
    Task DeleteAsync(Recipe recipe, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecipeProposal>> ListPendingProposalsAsync(Guid householdId, CancellationToken cancellationToken);
    Task<RecipeProposal?> GetProposalAsync(Guid proposalId, CancellationToken cancellationToken);
    Task SaveProposalAsync(RecipeProposal proposal, CancellationToken cancellationToken);
    Task<Guid?> GetIdempotentRecipeIdAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task MarkProposalAppliedAsync(Guid proposalId, string approvalReference, string idempotencyKey, Guid recipeId, CancellationToken cancellationToken);
}
