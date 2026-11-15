namespace Dinner.MealPlanningService.Domain;

public sealed record MealPlan(Guid Id, Guid HouseholdId, DateOnly StartDate, string Status, int Version, int Servings, MealAssignment[] Assignments, string[] Constraints, string[] Warnings, DateTimeOffset CreatedAt)
{
    public static MealPlan Create(PlanInput input) => new(Guid.NewGuid(), input.HouseholdId, input.StartDate, "Draft", 1, input.Servings, input.Assignments, input.Constraints ?? [], [], DateTimeOffset.UtcNow);
    public MealPlan Update(PlanInput input) => this with { StartDate = input.StartDate, Servings = input.Servings, Assignments = input.Assignments, Constraints = input.Constraints ?? [], Version = Version + 1, Warnings = [] };
    public MealPlan Approve() => this with { Status = "Approved", Version = Version + 1, Warnings = WarningsFor(this) };
    public MealPlan WithWarnings() => this with { Warnings = WarningsFor(this) };
    public static string[] WarningsFor(MealPlan plan) => plan.Assignments.Length < 7 ? ["Fewer than seven meals are planned."] : [];
}

public sealed record MealAssignment(DateOnly Date, string MealType, string RecipeId, bool Locked = false);
public sealed record PlanInput(Guid HouseholdId, DateOnly StartDate, int Servings, MealAssignment[] Assignments, string[]? Constraints);
