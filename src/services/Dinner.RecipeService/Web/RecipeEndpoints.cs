using Dinner.RecipeService.Application;
using Dinner.RecipeService.Application.Commands;
using Dinner.RecipeService.Domain;
using Dinner.ServiceDefaults;

namespace Dinner.RecipeService.Web;

public static class RecipeEndpoints
{
    private static readonly string[] ToolNames = ["find_recipes", "get_recipe", "create_recipe", "update_recipe", "delete_recipe"];

    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/recipes").WithTags("Recipes");
        api.MapGet("", ListAsync);
        api.MapGet("/{id:guid}", GetAsync);
        api.MapPost("", CreateAsync);
        api.MapPatch("/{id:guid}", UpdateAsync);
        api.MapDelete("/{id:guid}", DeleteAsync);

        app.MapGet("/mcp/resources/recipes/catalog", CatalogAsync);
        app.MapGet("/mcp/resources/recipes/{id:guid}", ResourceAsync);
        app.MapPost("/mcp/tools/find_recipes", FindAsync);
        app.MapPost("/mcp/tools/get_recipe", GetToolAsync);
        app.MapPost("/mcp/tools/create_recipe", CreateToolAsync);
        app.MapPost("/mcp/tools/update_recipe", UpdateToolAsync);
        app.MapPost("/mcp/tools/delete_recipe", DeleteToolAsync);
        app.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "recipes://catalog", "recipes://recipe/{recipeId}" }, tools = ToolNames }));
        app.MapMcpHttpServer(new("recipe-service", ToolNames, ["recipes://catalog", "recipes://recipe/{recipeId}"], RecipeToolDefinitions()));
    }

    private static async Task<IResult> ListAsync(Guid householdId, string? ingredient, string? cuisine, string? dietaryRestriction, string? tag, RecipeApplicationService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.SearchAsync(new(householdId, ingredient, cuisine, dietaryRestriction, tag), cancellationToken));

    private static async Task<IResult> GetAsync(Guid id, Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) =>
        await service.GetAsync(id, householdId, cancellationToken) is { } recipe ? Results.Ok(recipe) : Results.NotFound();

    private static async Task<IResult> CreateAsync(RecipeRequest request, RecipeApplicationService service, CancellationToken cancellationToken) =>
        ToCreateResult(await service.CreateAsync(new(request.ToDraft()), "user", cancellationToken));

    private static async Task<IResult> UpdateAsync(Guid id, RecipeRequest request, int expectedRevision, RecipeApplicationService service, CancellationToken cancellationToken) =>
        ToRecipeResult(await service.UpdateAsync(new(id, expectedRevision, request.ToDraft()), "user", cancellationToken));

    private static async Task<IResult> DeleteAsync(Guid id, Guid householdId, int expectedRevision, RecipeApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(new(householdId, id, expectedRevision), cancellationToken);
        return result.Recipe is not null ? Results.NoContent() : ToRecipeResult(result);
    }

    private static async Task<IResult> CatalogAsync(Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) =>
        Results.Ok(new { uri = "recipes://catalog", recipes = await service.SearchAsync(new(householdId, null), cancellationToken) });

    private static async Task<IResult> ResourceAsync(Guid id, Guid householdId, RecipeApplicationService service, CancellationToken cancellationToken) =>
        await service.GetAsync(id, householdId, cancellationToken) is { } recipe ? Results.Ok(new { uri = $"recipes://recipe/{id}", recipe }) : Results.NotFound();

    private static async Task<IResult> FindAsync(RecipeSearchRequest request, RecipeApplicationService service, CancellationToken cancellationToken) =>
        Results.Ok(new { structuredContent = await service.SearchAsync(new(request.HouseholdId, request.Ingredient, request.Cuisine, request.DietaryRestriction, request.Tag), cancellationToken) });

    private static async Task<IResult> GetToolAsync(RecipeLookupRequest request, RecipeApplicationService service, CancellationToken cancellationToken) =>
        await service.GetAsync(request.RecipeId, request.HouseholdId, cancellationToken) is { } recipe ? Results.Ok(new { structuredContent = recipe }) : Results.NotFound();

    private static async Task<IResult> CreateToolAsync(RecipeRequest request, RecipeApplicationService service, CancellationToken cancellationToken) =>
        ToToolResult(await service.CreateAsync(new(request.ToDraft()), "agent", cancellationToken));

    private static async Task<IResult> UpdateToolAsync(RecipeUpdateRequest request, RecipeApplicationService service, CancellationToken cancellationToken) =>
        ToToolResult(await service.UpdateAsync(new(request.RecipeId, request.ExpectedRevision, request.Recipe.ToDraft()), "agent", cancellationToken));

    private static async Task<IResult> DeleteToolAsync(RecipeDeleteRequest request, RecipeApplicationService service, CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(new(request.HouseholdId, request.RecipeId, request.ExpectedRevision), cancellationToken);
        return result.Recipe is not null ? Results.Ok(new { structuredContent = result.Recipe }) : ToRecipeResult(result);
    }

    private static IResult ToCreateResult(RecipeResult result) => result.Recipe is { } recipe ? Results.Created($"/api/v1/recipes/{recipe.Id}", recipe) : ToRecipeResult(result);

    private static IResult ToToolResult(RecipeResult result) => result.Recipe is { } recipe ? Results.Ok(new { structuredContent = recipe }) : ToRecipeResult(result);

    private static IResult ToRecipeResult(RecipeResult result) =>
        result.Errors is not null ? Results.ValidationProblem(result.Errors) :
        result.CurrentRevision is { } revision ? Results.Conflict(new { code = "revision_conflict", currentRevision = revision }) : Results.NotFound();

    private static IReadOnlyDictionary<string, McpToolDefinition> RecipeToolDefinitions()
    {
        var identifier = new { type = "string", format = "uuid" };
        var ingredient = new { type = "object", additionalProperties = false, properties = new { name = new { type = "string" }, quantity = new { type = "number", exclusiveMinimum = 0 }, unit = new { type = "string" }, pantryItemId = identifier, nutritionProductId = new { type = "string" }, unresolved = new { type = "boolean" } }, required = new[] { "name", "quantity", "unit" } };
        var step = new { type = "object", additionalProperties = false, properties = new { order = new { type = "integer", minimum = 1 }, instruction = new { type = "string", minLength = 1 } }, required = new[] { "instruction" } };
        var recipe = new { type = "object", additionalProperties = false, properties = new { householdId = identifier, title = new { type = "string" }, markdown = new { type = "string", description = "Canonical full recipe in Markdown." }, tags = new { type = "array", items = new { type = "string" } }, cuisine = new { type = "string" }, dietaryRestrictions = new { type = "array", items = new { type = "string" } }, instructions = new { type = "array", minItems = 1, items = step }, ingredients = new { type = "array", minItems = 1, items = ingredient } }, required = new[] { "householdId", "title", "markdown", "instructions", "ingredients" } };
        return new Dictionary<string, McpToolDefinition>
        {
            ["find_recipes"] = new("Find recipes by ingredient, cuisine, dietary restriction, or tag.", new { type = "object", additionalProperties = false, properties = new { householdId = identifier, ingredient = new { type = "string" }, cuisine = new { type = "string" }, dietaryRestriction = new { type = "string" }, tag = new { type = "string" } }, required = new[] { "householdId" } }),
            ["get_recipe"] = new("Read one recipe owned by a household.", new { type = "object", additionalProperties = false, properties = new { householdId = identifier, recipeId = identifier }, required = new[] { "householdId", "recipeId" } }),
            ["create_recipe"] = new("Create a recipe immediately. No approval step is required.", recipe),
            ["update_recipe"] = new("Update a recipe immediately at its expected revision. No approval step is required.", new { type = "object", additionalProperties = false, properties = new { recipeId = identifier, expectedRevision = new { type = "integer", minimum = 1 }, recipe }, required = new[] { "recipeId", "expectedRevision", "recipe" } }),
            ["delete_recipe"] = new("Delete a recipe immediately at its expected revision. No approval step is required.", new { type = "object", additionalProperties = false, properties = new { householdId = identifier, recipeId = identifier, expectedRevision = new { type = "integer", minimum = 1 } }, required = new[] { "householdId", "recipeId", "expectedRevision" } })
        };
    }
}

public sealed record IngredientRequest(string Name, decimal Quantity, string Unit, Guid? PantryItemId = null, string? NutritionProductId = null, bool Unresolved = false)
{
    public RecipeIngredient ToIngredient() => new(Name, Quantity, Unit, PantryItemId, NutritionProductId, Unresolved);
}

public sealed record RecipeStepRequest(int Order, string Instruction)
{
    public RecipeStep ToStep() => new(Order, Instruction);
}

public sealed record RecipeRequest(Guid HouseholdId, string Title, string Markdown, string[]? Tags, string? Cuisine, string[]? DietaryRestrictions, RecipeStepRequest[] Instructions, IngredientRequest[] Ingredients)
{
    public RecipeDraft ToDraft() => new(HouseholdId, Title, Markdown, Tags, Cuisine, DietaryRestrictions, Instructions?.Select(step => step.ToStep()).ToArray() ?? [], Ingredients?.Select(ingredient => ingredient.ToIngredient()).ToArray() ?? []);
}

public sealed record RecipeSearchRequest(Guid HouseholdId, string? Ingredient, string? Cuisine = null, string? DietaryRestriction = null, string? Tag = null);
public sealed record RecipeLookupRequest(Guid HouseholdId, Guid RecipeId);
public sealed record RecipeUpdateRequest(Guid RecipeId, int ExpectedRevision, RecipeRequest Recipe);
public sealed record RecipeDeleteRequest(Guid HouseholdId, Guid RecipeId, int ExpectedRevision);
