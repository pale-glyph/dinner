using Dinner.RecipeService.Application;
using Dinner.RecipeService.Application.Commands;
using Dinner.RecipeService.Domain;
using Dinner.ServiceDefaults;

namespace Dinner.RecipeService.Web;

public static class RecipeEndpoints
{
    private static readonly string[] ToolNames = ["find_recipes", "get_recipe", "propose_recipe_create", "propose_recipe_update", "propose_recipe_delete", "apply_approved_recipe_proposal"];

    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/recipes").WithTags("Recipes");
        api.MapGet("", ListAsync);
        api.MapGet("/proposals", ListProposalsAsync);
        api.MapGet("/{id:guid}", GetAsync);
        api.MapPost("", CreateAsync);
        api.MapPatch("/{id:guid}", UpdateAsync);
        api.MapDelete("/{id:guid}", DeleteAsync);

        app.MapGet("/mcp/resources/recipes/catalog", CatalogAsync);
        app.MapGet("/mcp/resources/recipes/{id:guid}", ResourceAsync);
        app.MapPost("/mcp/tools/find_recipes", FindAsync);
        app.MapPost("/mcp/tools/get_recipe", GetToolAsync);
        app.MapPost("/mcp/tools/propose_recipe_create", ProposeCreateAsync);
        app.MapPost("/mcp/tools/propose_recipe_update", ProposeUpdateAsync);
        app.MapPost("/mcp/tools/propose_recipe_delete", ProposeDeleteAsync);
        app.MapPost("/mcp/tools/apply_approved_recipe_proposal", ApplyProposalAsync);
        app.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "recipes://catalog", "recipes://recipe/{recipeId}" }, tools = ToolNames }));
        app.MapMcpHttpServer(new("recipe-service", ToolNames, ["recipes://catalog", "recipes://recipe/{recipeId}"], RecipeToolDefinitions()));
    }

    private static async Task<IResult> ListAsync(Guid householdId, string? ingredient, int? maxMinutes, string? mealType, string? tag, RecipeApplicationService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.SearchAsync(new RecipeSearchQuery(householdId, ingredient, maxMinutes, mealType, tag), cancellationToken));

    private static async Task<IResult> ListProposalsAsync(Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) => Results.Ok(await service.ListProposalsAsync(householdId, cancellationToken));
    private static async Task<IResult> GetAsync(Guid id, Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) => await service.GetAsync(id, householdId, cancellationToken) is { } recipe ? Results.Ok(recipe) : Results.NotFound();

    private static async Task<IResult> CreateAsync(RecipeRequest request, RecipeApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(new(request.ToDraft()), cancellationToken);
        return result.Recipe is { } recipe ? Results.Created($"/api/v1/recipes/{recipe.Id}", recipe) : Results.ValidationProblem(result.Errors!);
    }

    private static async Task<IResult> UpdateAsync(Guid id, RecipeRequest request, int expectedRevision, RecipeApplicationService service, CancellationToken cancellationToken) => ToRecipeResult(await service.UpdateAsync(new(id, expectedRevision, request.ToDraft()), cancellationToken));

    private static async Task<IResult> DeleteAsync(Guid id, Guid householdId, int expectedRevision, RecipeApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(new(householdId, id, expectedRevision), cancellationToken);
        return result.Recipe is not null ? Results.NoContent() : ToRecipeResult(result);
    }

    private static async Task<IResult> CatalogAsync(Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) => Results.Ok(new { uri = "recipes://catalog", recipes = await service.SearchAsync(new(householdId, null), cancellationToken) });
    private static async Task<IResult> ResourceAsync(Guid id, Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) => await service.GetAsync(id, householdId, cancellationToken) is { } recipe ? Results.Ok(new { uri = $"recipes://recipe/{id}", recipe }) : Results.NotFound();
    private static async Task<IResult> FindAsync(RecipeSearchRequest request, RecipeApplicationService service, CancellationToken cancellationToken) => Results.Ok(new { structuredContent = await service.SearchAsync(new(request.HouseholdId, request.Ingredient), cancellationToken) });
    private static async Task<IResult> GetToolAsync(RecipeLookupRequest request, RecipeApplicationService service, CancellationToken cancellationToken) => await service.GetAsync(request.RecipeId, request.HouseholdId, cancellationToken) is { } recipe ? Results.Ok(new { structuredContent = recipe }) : Results.NotFound();

    private static async Task<IResult> ProposeCreateAsync(RecipeRequest request, RecipeApplicationService service, CancellationToken cancellationToken) => ToProposalResult(await service.ProposeAsync(new("create", null, request.ToDraft()), cancellationToken));
    private static async Task<IResult> ProposeUpdateAsync(RecipeProposalRequest request, RecipeApplicationService service, CancellationToken cancellationToken) => ToProposalResult(await service.ProposeAsync(new("update", request.RecipeId, request.Recipe.ToDraft(), ExpectedRevision: request.ExpectedRevision), cancellationToken));
    private static async Task<IResult> ProposeDeleteAsync(RecipeDeleteProposalRequest request, RecipeApplicationService service, CancellationToken cancellationToken) => ToProposalResult(await service.ProposeAsync(new("delete", request.RecipeId, null, request.HouseholdId, request.ExpectedRevision), cancellationToken));

    private static async Task<IResult> ApplyProposalAsync(ApplyProposalRequest request, RecipeApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.ApplyProposalAsync(new(request.ProposalId, request.ApprovalReference, request.IdempotencyKey), cancellationToken);
        if (result.IdempotentRecipeId is { } recipeId) return Results.Ok(new { recipeId, idempotent = true });
        if (result.Recipe is { } recipe) return Results.Ok(new { structuredContent = recipe, approvalReference = request.ApprovalReference });
        if (result.Errors is not null) return Results.ValidationProblem(result.Errors);
        if (result.CurrentRevision is { } revision) return Results.Conflict(new { code = "revision_conflict", currentRevision = revision });
        return result.Conflict == "not_found" ? Results.NotFound() : Results.Conflict(new { code = result.Conflict });
    }

    private static IResult ToRecipeResult(RecipeResult result) =>
        result.Recipe is { } recipe ? Results.Ok(recipe) :
        result.Errors is not null ? Results.ValidationProblem(result.Errors) :
        result.CurrentRevision is { } revision ? Results.Conflict(new { code = "revision_conflict", currentRevision = revision }) : Results.NotFound();

    private static IResult ToProposalResult(ProposalResult result) => result.Proposal is { } proposal ? Results.Ok(new { structuredContent = proposal, requiresApproval = true }) : Results.ValidationProblem(result.Errors!);

    private static IReadOnlyDictionary<string, McpToolDefinition> RecipeToolDefinitions()
    {
        var identifier = new { type = "string", format = "uuid" };
        var ingredient = new { type = "object", additionalProperties = false, properties = new { name = new { type = "string", description = "Ingredient name." }, quantity = new { type = "number", exclusiveMinimum = 0 }, unit = new { type = "string", description = "Measurement unit, for example g, ml, or item." }, nutritionProductId = new { type = "string", description = "Optional nutrition product ID." }, unresolved = new { type = "boolean", description = "True when no nutrition product has been matched." } }, required = new[] { "name", "quantity", "unit" } };
        var recipe = new { type = "object", additionalProperties = false, properties = new { householdId = identifier, title = new { type = "string" }, description = new { type = "string" }, instructions = new { type = "string", description = "Ordered cooking instructions." }, totalMinutes = new { type = "integer", minimum = 0 }, servings = new { type = "integer", minimum = 1 }, tags = new { type = "array", items = new { type = "string" } }, ingredients = new { type = "array", minItems = 1, items = ingredient } }, required = new[] { "householdId", "title", "instructions", "totalMinutes", "servings", "ingredients" } };
        return new Dictionary<string, McpToolDefinition>
        {
            ["find_recipes"] = new("Find recipes in a household, optionally by ingredient.", new { type = "object", additionalProperties = false, properties = new { householdId = identifier, ingredient = new { type = "string" } }, required = new[] { "householdId" } }),
            ["get_recipe"] = new("Read one recipe owned by a household.", new { type = "object", additionalProperties = false, properties = new { householdId = identifier, recipeId = identifier }, required = new[] { "householdId", "recipeId" } }),
            ["propose_recipe_create"] = new("Create a recipe proposal. It must be explicitly approved with apply_approved_recipe_proposal before saving.", recipe),
            ["propose_recipe_update"] = new("Propose an update to the current recipe revision. Approval is required before saving.", new { type = "object", additionalProperties = false, properties = new { recipeId = identifier, expectedRevision = new { type = "integer", minimum = 1 }, recipe }, required = new[] { "recipeId", "expectedRevision", "recipe" } }),
            ["propose_recipe_delete"] = new("Propose deleting a recipe. Approval is required before deletion.", new { type = "object", additionalProperties = false, properties = new { householdId = identifier, recipeId = identifier, expectedRevision = new { type = "integer", minimum = 1 } }, required = new[] { "householdId", "recipeId", "expectedRevision" } }),
            ["apply_approved_recipe_proposal"] = new("Apply a pending recipe proposal after explicit user approval.", new { type = "object", additionalProperties = false, properties = new { proposalId = identifier, approvalReference = new { type = "string", minLength = 1 }, idempotencyKey = new { type = "string", minLength = 1 } }, required = new[] { "proposalId", "approvalReference", "idempotencyKey" } })
        };
    }
}

public sealed record IngredientRequest(string Name, decimal Quantity, string Unit, string? NutritionProductId, bool Unresolved = false)
{
    public RecipeIngredient ToIngredient() => new(Name, Quantity, Unit, NutritionProductId, Unresolved);
}

public sealed record RecipeRequest(Guid HouseholdId, string Title, string? Description, string Instructions, int TotalMinutes, int Servings, string[]? Tags, IngredientRequest[] Ingredients)
{
    public RecipeDraft ToDraft() => new(HouseholdId, Title, Description, Instructions, TotalMinutes, Servings, Tags, Ingredients.Select(ingredient => ingredient.ToIngredient()).ToArray());
}

public sealed record RecipeSearchRequest(Guid HouseholdId, string? Ingredient);
public sealed record RecipeLookupRequest(Guid HouseholdId, Guid RecipeId);
public sealed record RecipeProposalRequest(Guid RecipeId, int ExpectedRevision, RecipeRequest Recipe);
public sealed record RecipeDeleteProposalRequest(Guid HouseholdId, Guid RecipeId, int ExpectedRevision);
public sealed record ApplyProposalRequest(Guid ProposalId, string ApprovalReference, string IdempotencyKey);
