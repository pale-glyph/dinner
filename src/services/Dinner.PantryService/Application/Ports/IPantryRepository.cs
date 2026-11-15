using Dinner.PantryService.Domain;

namespace Dinner.PantryService.Application.Ports;

public interface IPantryRepository
{
    Task<IReadOnlyList<PantryItem>> ListAsync(Guid householdId, CancellationToken cancellationToken);
    Task<PantryItem?> GetAsync(Guid itemId, CancellationToken cancellationToken);
    Task CreateAsync(PantryItem item, CancellationToken cancellationToken);
    Task UpdateAsync(PantryItem item, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid itemId, CancellationToken cancellationToken);
    Task LogAdjustmentAsync(Guid itemId, string kind, decimal quantity, string unit, string? note, CancellationToken cancellationToken);
}

public interface IPantryDatabaseInitializer { Task InitializeAsync(CancellationToken cancellationToken); }
public interface IClock { DateOnly Today { get; } }
