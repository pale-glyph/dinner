using Dinner.PantryService.Application.Commands;
using Dinner.PantryService.Application.Services;
using Dinner.PantryService.Domain;
using Dinner.ServiceDefaults;

namespace Dinner.PantryService.Web;

public static class PantryEndpoints
{
    public static void MapPantryEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/pantry/items").WithTags("Pantry");
        api.MapGet("", async (Guid householdId, PantryApplicationService service, CancellationToken ct) => Results.Ok(await service.ListAsync(householdId, ct)));
        api.MapGet("/expiring", async (Guid householdId, int? days, PantryApplicationService service, CancellationToken ct) => Results.Ok(await service.GetExpiringAsync(householdId, days ?? 7, ct)));
        api.MapGet("/low-stock", async (Guid householdId, PantryApplicationService service, CancellationToken ct) => Results.Ok(await service.GetLowStockAsync(householdId, ct)));
        api.MapGet("/{id:guid}", async (Guid id, PantryApplicationService service, CancellationToken ct) => await service.GetAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());
        api.MapPost("", async (PantryItemRequest input, PantryApplicationService service, CancellationToken ct) => ToCreated(await service.CreateAsync(input.ToCommand(), ct)));
        api.MapPut("/{id:guid}", async (Guid id, PantryItemRequest input, PantryApplicationService service, CancellationToken ct) => ToResult(await service.UpdateAsync(id, input.ToCommand(), ct)));
        api.MapDelete("/{id:guid}", async (Guid id, PantryApplicationService service, CancellationToken ct) => await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());
        api.MapPost("/{id:guid}/adjustments", async (Guid id, AdjustmentRequest input, PantryApplicationService service, CancellationToken ct) => ToAdjustmentResult(await service.AdjustAsync(id, input.ToCommand(), ct)));

        app.MapGet("/mcp/resources/pantry/items", async (Guid householdId, PantryApplicationService service, CancellationToken ct) => Results.Ok(new { uri = "pantry://items", items = await service.ListAsync(householdId, ct) }));
        app.MapGet("/mcp/resources/pantry/expiring", async (Guid householdId, PantryApplicationService service, CancellationToken ct) => Results.Ok(new { uri = "pantry://expiring", items = await service.GetExpiringAsync(householdId, 7, ct) }));
        app.MapGet("/mcp/resources/pantry/low-stock", async (Guid householdId, PantryApplicationService service, CancellationToken ct) => Results.Ok(new { uri = "pantry://low-stock", items = await service.GetLowStockAsync(householdId, ct) }));
        app.MapPost("/mcp/tools/search_pantry", async (SearchRequest input, PantryApplicationService service, CancellationToken ct) => Results.Ok(new { structuredContent = await service.SearchAsync(input.HouseholdId, input.Query, ct), summary = "Pantry search completed." }));
        app.MapPost("/mcp/tools/get_pantry_item", async (ItemToolRequest input, PantryApplicationService service, CancellationToken ct) => await service.GetAsync(input.ItemId, ct) is { } item && item.HouseholdId == input.HouseholdId ? Results.Ok(new { structuredContent = item }) : Results.NotFound());
        app.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "pantry://items", "pantry://expiring", "pantry://low-stock" }, tools = new[] { "search_pantry", "get_pantry_item", "propose_pantry_adjustments", "apply_pantry_adjustments" } }));
        app.MapPost("/mcp/tools/propose_pantry_adjustments", (McpAdjustmentsRequest input) => Results.Ok(new { proposal = input.Adjustments, requiresApproval = true, summary = $"Proposed {input.Adjustments.Length} pantry adjustment(s)." }));
        app.MapPost("/mcp/tools/apply_pantry_adjustments", async (McpAdjustmentsRequest input, PantryApplicationService service, CancellationToken ct) => await ApplyAdjustments(input, service, ct));
        app.MapMcpHttpServer(new("pantry-service", ["search_pantry", "get_pantry_item", "propose_pantry_adjustments", "apply_pantry_adjustments"], ["pantry://items", "pantry://expiring", "pantry://low-stock"]));
    }

    private static IResult ToCreated(PantryResult result) => result.Item is { } item ? Results.Created($"/api/v1/pantry/items/{item.Id}", item) : ToResult(result);
    private static IResult ToResult(PantryResult result) => result.Errors is not null ? Results.ValidationProblem(result.Errors) : Results.NotFound();
    private static IResult ToAdjustmentResult(PantryResult result) => result.Item is { } item ? Results.Ok(item) : result.Errors is not null ? Results.ValidationProblem(result.Errors) : result.Conflict is not null ? Results.Conflict(new { code = result.Conflict, detail = "Consumption cannot make pantry stock negative." }) : Results.NotFound();

    private static async Task<IResult> ApplyAdjustments(McpAdjustmentsRequest input, PantryApplicationService service, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.ApprovalReference)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["approvalReference"] = ["An approval reference is required."] });
        var applied = new List<PantryItem>();
        foreach (var change in input.Adjustments)
        {
            var item = await service.GetAsync(change.ItemId, ct);
            if (item is null || item.HouseholdId != input.HouseholdId) return Results.NotFound();
            var result = await service.AdjustAsync(change.ItemId, change.ToCommand(), ct);
            if (result.Item is { } updated) { applied.Add(updated); continue; }
            if (result.Errors is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["adjustments"] = ["Each adjustment needs a compatible unit, positive quantity, and consume or restore kind."] });
            if (result.Conflict is not null) return Results.Conflict(new { code = result.Conflict, itemId = change.ItemId });
            return Results.NotFound();
        }
        return Results.Ok(new { structuredContent = applied, approvalReference = input.ApprovalReference });
    }

    public sealed record PantryItemRequest(Guid HouseholdId, string? Name, decimal Quantity, string? Unit, string? Location, DateOnly? ExpiryDate, decimal? LowStockThreshold, string? ProductCode, string? FoodProductId)
    { public PantryItemCommand ToCommand() => new(HouseholdId, Name, Quantity, Unit, Location, ExpiryDate, LowStockThreshold, ProductCode, FoodProductId); }
    public sealed record AdjustmentRequest(string Kind, decimal Quantity, string? Unit, string? Note)
    { public AdjustmentCommand ToCommand() => new(Kind, Quantity, Unit, Note); }
    public sealed record SearchRequest(Guid HouseholdId, string? Query, string? CorrelationId);
    public sealed record ItemToolRequest(Guid HouseholdId, Guid ItemId, string? CorrelationId);
    public sealed record McpAdjustmentRequest(Guid ItemId, string Kind, decimal Quantity, string? Unit, string? Note)
    { public AdjustmentCommand ToCommand() => new(Kind, Quantity, Unit, Note); }
    public sealed record McpAdjustmentsRequest(Guid HouseholdId, McpAdjustmentRequest[] Adjustments, string? ApprovalReference, string? CorrelationId);
}
