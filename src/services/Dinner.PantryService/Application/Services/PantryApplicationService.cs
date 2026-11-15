using Dinner.PantryService.Application.Commands;
using Dinner.PantryService.Application.Ports;
using Dinner.PantryService.Domain;

namespace Dinner.PantryService.Application.Services;

public sealed record PantryResult(PantryItem? Item = null, IReadOnlyDictionary<string, string[]>? Errors = null, string? Conflict = null);

public sealed class PantryApplicationService(IPantryRepository repository, IClock clock)
{
    public Task<IReadOnlyList<PantryItem>> ListAsync(Guid householdId, CancellationToken cancellationToken) => repository.ListAsync(householdId, cancellationToken);
    public Task<PantryItem?> GetAsync(Guid itemId, CancellationToken cancellationToken) => repository.GetAsync(itemId, cancellationToken);
    public Task<bool> DeleteAsync(Guid itemId, CancellationToken cancellationToken) => repository.DeleteAsync(itemId, cancellationToken);

    public async Task<IReadOnlyList<PantryItem>> GetExpiringAsync(Guid householdId, int days, CancellationToken cancellationToken) =>
        (await repository.ListAsync(householdId, cancellationToken)).Where(item => item.ExpiryDate is not null && item.ExpiryDate <= clock.Today.AddDays(days)).ToArray();

    public async Task<IReadOnlyList<PantryItem>> GetLowStockAsync(Guid householdId, CancellationToken cancellationToken) =>
        (await repository.ListAsync(householdId, cancellationToken)).Where(item => item.LowStockThreshold is not null && item.Quantity <= item.LowStockThreshold).ToArray();

    public async Task<IReadOnlyList<PantryItem>> SearchAsync(Guid householdId, string? query, CancellationToken cancellationToken) =>
        (await repository.ListAsync(householdId, cancellationToken)).Where(item => string.IsNullOrWhiteSpace(query) || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

    public async Task<PantryResult> CreateAsync(PantryItemCommand command, CancellationToken cancellationToken)
    {
        var result = Validate(command);
        if (result.Errors is not null) return result;
        await repository.CreateAsync(result.Item!, cancellationToken);
        return result;
    }

    public async Task<PantryResult> UpdateAsync(Guid itemId, PantryItemCommand command, CancellationToken cancellationToken)
    {
        if (await repository.GetAsync(itemId, cancellationToken) is null) return new();
        var result = Validate(command, itemId);
        if (result.Errors is not null) return result;
        await repository.UpdateAsync(result.Item!, cancellationToken);
        return result;
    }

    public async Task<PantryResult> AdjustAsync(Guid itemId, AdjustmentCommand command, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(itemId, cancellationToken);
        if (item is null) return new();
        if (command.Quantity <= 0 || command.Kind is not ("consume" or "restore")) return new(Errors: new Dictionary<string, string[]> { ["adjustment"] = ["Use a positive quantity and either consume or restore."] });
        if (!PantryUnits.TryNormalize(command.Quantity, command.Unit ?? item.Unit, out var amount, out var unit) || unit != item.Unit) return new(Errors: new Dictionary<string, string[]> { ["unit"] = [$"Adjustment must be compatible with {item.Unit}."] });
        var quantity = command.Kind == "consume" ? item.Quantity - amount : item.Quantity + amount;
        if (quantity < 0) return new(Conflict: "insufficient_stock");
        var adjusted = item.WithQuantity(quantity);
        await repository.UpdateAsync(adjusted, cancellationToken);
        await repository.LogAdjustmentAsync(itemId, command.Kind, amount, unit, command.Note, cancellationToken);
        return new(adjusted);
    }

    private static PantryResult Validate(PantryItemCommand command, Guid? id = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (command.HouseholdId == Guid.Empty) errors["householdId"] = ["A household is required."];
        if (string.IsNullOrWhiteSpace(command.Name)) errors["name"] = ["Name is required."];
        if (!PantryUnits.TryNormalize(command.Quantity, command.Unit, out var quantity, out var unit)) errors["unit"] = ["Use a positive quantity and a supported unit: g, kg, ml, l, or item."];
        if (command.LowStockThreshold < 0) errors["lowStockThreshold"] = ["Threshold cannot be negative."];
        if (errors.Count > 0) return new(Errors: errors);
        decimal? threshold = null;
        if (command.LowStockThreshold is not null) { PantryUnits.TryNormalize(command.LowStockThreshold.Value, command.Unit, out var normalized, out _); threshold = normalized; }
        return new(new PantryItem(id ?? Guid.NewGuid(), command.HouseholdId, command.Name!.Trim(), quantity, unit, command.Location?.Trim(), command.ExpiryDate, threshold, command.ProductCode?.Trim(), command.FoodProductId?.Trim()));
    }
}
