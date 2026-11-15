namespace Dinner.HouseholdService.Domain;

public sealed record Household(Guid Id, string Name, int Servings, string[] DietaryPreferences, string[] Allergies, string[] DislikedFoods)
{
    public Household UpdatePreferences(string[]? dietaryPreferences, string[]? allergies, string[]? dislikedFoods) => this with
    {
        DietaryPreferences = dietaryPreferences ?? DietaryPreferences,
        Allergies = allergies ?? Allergies,
        DislikedFoods = dislikedFoods ?? DislikedFoods
    };
}

public sealed record HouseholdMembership(Guid Id, string Name, string Role);
public sealed record HouseholdMember(Guid Id, string? DisplayName, string? Email, string Role);
public sealed record AccountContext(Guid Id, string ClerkUserId, string? Email, string? DisplayName, IReadOnlyList<HouseholdMembership> Households);
