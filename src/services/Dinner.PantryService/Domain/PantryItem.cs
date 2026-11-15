namespace Dinner.PantryService.Domain;

public sealed record PantryItem(Guid Id, Guid HouseholdId, string Name, decimal Quantity, string Unit, string? Location, DateOnly? ExpiryDate, decimal? LowStockThreshold, string? ProductCode, string? FoodProductId)
{
    public PantryItem WithQuantity(decimal quantity) => this with { Quantity = quantity };
}

public static class PantryUnits
{
    public static bool TryNormalize(decimal quantity, string? value, out decimal normalizedQuantity, out string unit)
    {
        normalizedQuantity = 0; unit = string.Empty;
        if (quantity <= 0 || string.IsNullOrWhiteSpace(value)) return false;
        switch (value.Trim().ToLowerInvariant())
        {
            case "g" or "gram" or "grams": normalizedQuantity = quantity; unit = "g"; return true;
            case "kg" or "kilogram" or "kilograms": normalizedQuantity = quantity * 1000; unit = "g"; return true;
            case "ml" or "millilitre" or "millilitres": normalizedQuantity = quantity; unit = "ml"; return true;
            case "l" or "litre" or "litres": normalizedQuantity = quantity * 1000; unit = "ml"; return true;
            case "item" or "items" or "each": normalizedQuantity = quantity; unit = "item"; return true;
            default: return false;
        }
    }
}
