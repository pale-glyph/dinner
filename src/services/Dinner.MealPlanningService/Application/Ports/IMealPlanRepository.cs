using Dinner.MealPlanningService.Domain;

namespace Dinner.MealPlanningService.Application.Ports;

public interface IMealPlanRepository
{
    Task<MealPlan?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MealPlan>> ListAsync(Guid householdId, CancellationToken cancellationToken);
    Task SaveAsync(MealPlan plan, CancellationToken cancellationToken);
}

public interface IMealPlanDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
