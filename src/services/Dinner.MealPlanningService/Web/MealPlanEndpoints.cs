using Dinner.MealPlanningService.Application;
using Dinner.MealPlanningService.Domain;
using Dinner.ServiceDefaults;
namespace Dinner.MealPlanningService.Web;

public static class MealPlanEndpoints
{
    public static IEndpointRouteBuilder MapMealPlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1/meal-plans").WithTags("Meal plans");
        api.MapGet("", async (Guid householdId, MealPlanService service, CancellationToken ct) => Results.Ok(await service.ListAsync(householdId, ct)));
        api.MapGet("/{id:guid}", async (Guid id, MealPlanService service, CancellationToken ct) => await service.GetAsync(id, ct) is { } plan ? Results.Ok(plan) : Results.NotFound());
        api.MapPost("", async (PlanInput input, MealPlanService service, CancellationToken ct) => ToCreated(await service.CreateAsync(input, false, ct)));
        api.MapPut("/{id:guid}", async (Guid id, PlanInput input, int version, MealPlanService service, CancellationToken ct) => ToResult(await service.UpdateAsync(id, input, version, ct)));
        api.MapPost("/{id:guid}/validate", async (Guid id, MealPlanService service, CancellationToken ct) => await service.GetAsync(id, ct) is { } plan ? Results.Ok(new { planId = id, warnings = MealPlan.WarningsFor(plan), valid = MealPlan.WarningsFor(plan).Length == 0 }) : Results.NotFound());
        api.MapPost("/{id:guid}/approve", async (Guid id, Approval input, MealPlanService service, CancellationToken ct) => { var result = await service.ApproveAsync(id, input.Version, ct); return result.Plan is { } plan ? Results.Ok(new { plan, approvalReference = input.ApprovalReference ?? $"meal-plan:{id}:v{plan.Version}" }) : ToResult(result); });
        endpoints.MapGet("/mcp/resources/meal-plans/current", async (Guid householdId, MealPlanService service, CancellationToken ct) => Results.Ok(new { uri = "meal-plans://current", plans = await service.ListAsync(householdId, ct) }));
        endpoints.MapPost("/mcp/tools/get_meal_plan", async (PlanLookup input, MealPlanService service, CancellationToken ct) => await service.GetAsync(input.PlanId, ct) is { } plan && plan.HouseholdId == input.HouseholdId ? Results.Ok(new { structuredContent = plan }) : Results.NotFound());
        endpoints.MapPost("/mcp/tools/propose_meal_plan", async (PlanInput input, MealPlanService service, CancellationToken ct) => { var result = await service.CreateAsync(input, true, ct); return result.Plan is { } plan ? Results.Ok(new { structuredContent = plan, requiresApproval = true, summary = "Meal-plan draft created; approval is required before commitment." }) : ToResult(result); });
        endpoints.MapPost("/mcp/tools/commit_meal_plan", async (CommitPlan input, MealPlanService service, CancellationToken ct) => { if (string.IsNullOrWhiteSpace(input.ApprovalReference)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["approvalReference"] = ["An approval reference is required."] }); var result = await service.ApproveAsync(input.PlanId, input.Version, ct); return result.Plan is { } plan ? Results.Ok(new { structuredContent = plan, approvalReference = input.ApprovalReference }) : ToResult(result); });
        endpoints.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "meal-plans://current" }, tools = new[] { "get_meal_plan", "propose_meal_plan", "commit_meal_plan" } }));
        return endpoints;
    }
    private static IResult ToCreated(PlanResult result) => result.Plan is { } plan ? Results.Created($"/api/v1/meal-plans/{plan.Id}", plan) : ToResult(result);
    private static IResult ToResult(PlanResult result) => result.Errors is not null ? Results.ValidationProblem(result.Errors) : result.Conflict is not null ? Results.Conflict(new { code = result.Conflict, detail = "Only the current draft version can be edited." }) : Results.NotFound();
    public sealed record Approval(int Version, string? ApprovalReference);
    public sealed record PlanLookup(Guid HouseholdId, Guid PlanId);
    public sealed record CommitPlan(Guid PlanId, int Version, string? ApprovalReference);
}
