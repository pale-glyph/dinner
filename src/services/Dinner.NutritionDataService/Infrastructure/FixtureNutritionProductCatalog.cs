using Dinner.NutritionDataService.Application.Ports;
using Dinner.NutritionDataService.Domain;

namespace Dinner.NutritionDataService.Infrastructure;

public sealed class FixtureNutritionProductCatalog : INutritionProductCatalog
{
    private static readonly IReadOnlyList<NutritionProduct> Products =
    [
        new("off-tomatoes", "Tinned chopped tomatoes", "5012345678900", 100m, "g", new Nutrients(23, 1.1m, 0.2m, 3.6m, 1.5m, 0.1m), new Provenance("Open Food Facts fixture", "2026-11-14", "ODbL", "off-fixture-v1")),
        new("off-chickpeas", "Chickpeas, tinned", "5000000000001", 100m, "g", new Nutrients(128, 7m, 2.1m, 17m, 5m, 0.4m), new Provenance("Open Food Facts fixture", "2026-11-14", "ODbL", "off-fixture-v1"))
    ];

    private static readonly NutritionImport Import = new(
        "Open Food Facts fixture",
        "off-fixture-v1",
        "ODbL",
        "2026-11-14T00:00:00Z",
        Products.Count,
        "Representative local subset; values are not medical advice.");

    public IReadOnlyList<NutritionProduct> GetAll() => Products;

    public NutritionImport GetImport() => Import;
}
