using Dinner.NutritionDataService.Application;
using Dinner.ServiceDefaults;

namespace Dinner.NutritionDataService.Web;

public static class NutritionEndpoints
{
    private static readonly string[] ToolNames = ["find_nutrition_product", "get_nutrition_product"];
    private static readonly string[] ResourceUris = ["nutrition://products"];

    private static readonly object ImportManifest = new
    {
        source = "Open Food Facts fixture",
        sourceVersion = "off-fixture-v1",
        licence = "ODbL",
        importedAt = "2026-11-14T00:00:00Z",
        productCount = 2,
        notes = "Representative local subset; values are not medical advice."
    };

    public static IEndpointRouteBuilder MapNutritionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/nutrition/imports/fixture", () => Results.Ok(ImportManifest));
        endpoints.MapPost("/api/v1/nutrition/imports/fixture", () => Results.Ok(ImportManifest));
        endpoints.MapGet("/api/v1/nutrition/products", (string? query, string? barcode, NutritionCatalogService service) => Results.Ok(service.GetProducts(query, barcode)));
        endpoints.MapGet("/api/v1/nutrition/products/{id}", (string id, NutritionCatalogService service) => service.GetProduct(id) is { } product ? Results.Ok(product) : Results.NotFound());
        endpoints.MapGet("/mcp/resources/nutrition/products", (NutritionCatalogService service) => Results.Ok(new { uri = "nutrition://products", products = service.GetProducts(null, null) }));
        endpoints.MapPost("/mcp/tools/find_nutrition_product", (NutritionSearch input, NutritionCatalogService service) => Results.Ok(new { structuredContent = service.Search(input.Query), notice = "Nutrition data is a fixture and is not medical advice." }));
        endpoints.MapPost("/mcp/tools/get_nutrition_product", (NutritionLookup input, NutritionCatalogService service) => service.GetProduct(input.ProductId) is { } product ? Results.Ok(new { structuredContent = product, notice = "Values are per serving basis and may be incomplete." }) : Results.NotFound());
        endpoints.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = ResourceUris, tools = ToolNames }));
        endpoints.MapMcpHttpServer(new McpServerDefinition("nutrition-data-service", ToolNames, ResourceUris));
        return endpoints;
    }

    public sealed record NutritionSearch(string? Query);
    public sealed record NutritionLookup(string ProductId);
}
