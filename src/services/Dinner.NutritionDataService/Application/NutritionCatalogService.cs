using Dinner.NutritionDataService.Domain;
using Dinner.NutritionDataService.Application.Ports;

namespace Dinner.NutritionDataService.Application;

public sealed class NutritionCatalogService(INutritionProductCatalog catalog)
{
    public NutritionImport GetImport() => catalog.GetImport();

    public IReadOnlyList<NutritionProduct> GetProducts(string? query, string? barcode) =>
        catalog.GetAll()
            .Where(product =>
                (string.IsNullOrWhiteSpace(query) || product.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(barcode) || product.Barcode == barcode))
            .ToArray();

    public NutritionProduct? GetProduct(string id) => catalog.GetAll().FirstOrDefault(product => product.Id == id);

    public IReadOnlyList<NutritionProduct> Search(string? query) =>
        catalog.GetAll()
            .Where(product => product.Name.Contains(query ?? string.Empty, StringComparison.OrdinalIgnoreCase) || product.Barcode == query)
            .ToArray();
}
