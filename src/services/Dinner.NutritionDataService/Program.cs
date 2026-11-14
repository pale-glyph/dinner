using Dinner.ServiceDefaults;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build().MapDinnerServiceDefaults("nutrition-data-service");
app.UseCors();
var products = new[]
{
    new NutritionProduct("off-tomatoes", "Tinned chopped tomatoes", "5012345678900", 100m, "g", new Nutrients(23, 1.1m, 0.2m, 3.6m, 1.5m, 0.1m), new Provenance("Open Food Facts fixture", "2026-11-14", "ODbL", "off-fixture-v1")),
    new NutritionProduct("off-chickpeas", "Chickpeas, tinned", "5000000000001", 100m, "g", new Nutrients(128, 7m, 2.1m, 17m, 5m, 0.4m), new Provenance("Open Food Facts fixture", "2026-11-14", "ODbL", "off-fixture-v1"))
};
var importManifest = new { source = "Open Food Facts fixture", sourceVersion = "off-fixture-v1", licence = "ODbL", importedAt = "2026-11-14T00:00:00Z", productCount = products.Length, notes = "Representative local subset; values are not medical advice." };
app.MapGet("/api/v1/nutrition/imports/fixture", () => Results.Ok(importManifest));
app.MapPost("/api/v1/nutrition/imports/fixture", () => Results.Ok(importManifest));
app.MapGet("/api/v1/nutrition/products", (string? query, string? barcode) => Results.Ok(products.Where(p => (string.IsNullOrWhiteSpace(query) || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) && (string.IsNullOrWhiteSpace(barcode) || p.Barcode == barcode))));
app.MapGet("/api/v1/nutrition/products/{id}", (string id) => products.FirstOrDefault(p => p.Id == id) is { } product ? Results.Ok(product) : Results.NotFound());
app.MapGet("/mcp/resources/nutrition/products", () => Results.Ok(new { uri = "nutrition://products", products }));
app.MapPost("/mcp/tools/find_nutrition_product", (NutritionSearch input) => Results.Ok(new { structuredContent = products.Where(p => p.Name.Contains(input.Query ?? "", StringComparison.OrdinalIgnoreCase) || p.Barcode == input.Query).ToArray(), notice = "Nutrition data is a fixture and is not medical advice." }));
app.MapPost("/mcp/tools/get_nutrition_product", (NutritionLookup input) => products.FirstOrDefault(p => p.Id == input.ProductId) is { } product ? Results.Ok(new { structuredContent = product, notice = "Values are per serving basis and may be incomplete." }) : Results.NotFound());
app.MapGet("/mcp/capabilities", () => Results.Ok(new { version = "v1", resources = new[] { "nutrition://products" }, tools = new[] { "find_nutrition_product", "get_nutrition_product" } }));
app.Run();

record NutritionProduct(string Id, string Name, string Barcode, decimal ServingQuantity, string ServingUnit, Nutrients Nutrients, Provenance Provenance);
record Nutrients(decimal EnergyKcal, decimal ProteinG, decimal FatG, decimal CarbohydrateG, decimal FibreG, decimal SaltG);
record Provenance(string Source, string ImportedAt, string Licence, string SourceVersion);
record NutritionSearch(string? Query);
record NutritionLookup(string ProductId);
