using Dinner.MealPlanningService.Domain;

namespace Dinner.MealPlanningService.Application.Commands;

public sealed record PlanCommand(Guid HouseholdId, DateOnly StartDate, int Servings, MealAssignment[] Assignments, string[]? Constraints);
