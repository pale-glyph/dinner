using Dinner.MealPlanningService.Application.Commands;
using Dinner.MealPlanningService.Application.Ports;
using Dinner.MealPlanningService.Domain;
namespace Dinner.MealPlanningService.Application;

public sealed record PlanResult(MealPlan? Plan, IReadOnlyDictionary<string, string[]>? Errors = null, string? Conflict = null);
public sealed class MealPlanService(IMealPlanRepository repository, IClock clock)
{
    public Task<IReadOnlyList<MealPlan>> ListAsync(Guid householdId, CancellationToken ct) => repository.ListAsync(householdId, ct);
    public Task<MealPlan?> GetAsync(Guid id, CancellationToken ct) => repository.GetAsync(id, ct);
    public async Task<PlanResult> CreateAsync(PlanCommand input, bool withWarnings, CancellationToken ct) { var errors = Validate(input); if (errors.Count > 0) return new(null, errors); var plan = MealPlan.Create(input.HouseholdId, input.StartDate, input.Servings, input.Assignments, input.Constraints ?? [], clock.UtcNow); if (withWarnings) plan = plan.WithWarnings(); await repository.SaveAsync(plan, ct); return new(plan); }
    public async Task<PlanResult> UpdateAsync(Guid id, PlanCommand input, int version, CancellationToken ct) { var plan = await repository.GetAsync(id, ct); if (plan is null) return new(null); if (plan.Status != "Draft" || plan.Version != version) return new(null, Conflict: "version_conflict"); var errors = Validate(input); if (errors.Count > 0) return new(null, errors); plan = plan.Update(input.StartDate, input.Servings, input.Assignments, input.Constraints ?? []); await repository.SaveAsync(plan, ct); return new(plan); }
    public async Task<PlanResult> ApproveAsync(Guid id, int version, CancellationToken ct) { var plan = await repository.GetAsync(id, ct); if (plan is null) return new(null); if (plan.Status != "Draft" || plan.Version != version) return new(null, Conflict: "version_conflict"); plan = plan.Approve(); await repository.SaveAsync(plan, ct); return new(plan); }
    private static Dictionary<string, string[]> Validate(PlanCommand input) { var errors = new Dictionary<string, string[]>(); if (input.HouseholdId == Guid.Empty) errors["householdId"] = ["A household is required."]; if (input.Servings <= 0) errors["servings"] = ["Servings must be positive."]; if (input.Assignments is null || input.Assignments.Length == 0) errors["assignments"] = ["At least one meal assignment is required."]; else if (input.Assignments.Any(a => string.IsNullOrWhiteSpace(a.RecipeId) || a.Date < input.StartDate || a.Date > input.StartDate.AddDays(6)) || input.Assignments.GroupBy(a => new { a.Date, a.MealType }).Any(group => group.Count() > 1)) errors["assignments"] = ["Assignments must have a recipe, fall within the week, and not duplicate a meal slot."]; return errors; }
}
