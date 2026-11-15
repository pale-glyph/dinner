namespace Dinner.PantryService.Application.Commands;

public sealed record PantryItemCommand(Guid HouseholdId, string? Name, decimal Quantity, string? Unit, string? Location, DateOnly? ExpiryDate, decimal? LowStockThreshold, string? ProductCode, string? FoodProductId);
public sealed record AdjustmentCommand(string Kind, decimal Quantity, string? Unit, string? Note);
