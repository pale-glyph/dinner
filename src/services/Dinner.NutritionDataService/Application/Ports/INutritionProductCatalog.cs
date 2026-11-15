using Dinner.NutritionDataService.Domain;

namespace Dinner.NutritionDataService.Application.Ports;

public interface INutritionProductCatalog
{
    IReadOnlyList<NutritionProduct> GetAll();

    NutritionImport GetImport();
}
