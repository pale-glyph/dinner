namespace Dinner.MealPlanningService.Domain;

public sealed record MealPlan(Guid Id, Guid HouseholdId, DateOnly StartDate, string Status, int Version, int Servings, MealAssignment[] Assignments, string[] Constraints, string[] Warnings, DateTimeOffset CreatedAt)
{
    public static MealPlan Create(Guid householdId, DateOnly startDate, int servings, MealAssignment[] assignments, string[] constraints, DateTimeOffset createdAt) => new(Guid.NewGuid(), householdId, startDate, "Draft", 1, servings, assignments, constraints, [], createdAt);
    public MealPlan Update(DateOnly startDate, int servings, MealAssignment[] assignments, string[] constraints) => this with { StartDate = startDate, Servings = servings, Assignments = assignments, Constraints = constraints, Version = Version + 1, Warnings = [] };
    public MealPlan Approve() => this with { Status = "Approved", Version = Version + 1, Warnings = WarningsFor(this) };
    public MealPlan WithWarnings() => this with { Warnings = WarningsFor(this) };
    public static string[] WarningsFor(MealPlan plan) => plan.Assignments.Length < 7 ? ["Fewer than seven meals are planned."] : [];
}

public sealed record MealAssignment(DateOnly Date, string MealType, string RecipeId, bool Locked = false);
