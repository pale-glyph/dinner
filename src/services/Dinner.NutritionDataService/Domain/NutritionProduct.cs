namespace Dinner.NutritionDataService.Domain;

public sealed record NutritionProduct(
    string Id,
    string Name,
    string Barcode,
    decimal ServingQuantity,
    string ServingUnit,
    Nutrients Nutrients,
    Provenance Provenance);

public sealed record Nutrients(decimal EnergyKcal, decimal ProteinG, decimal FatG, decimal CarbohydrateG, decimal FibreG, decimal SaltG);

public sealed record Provenance(string Source, string ImportedAt, string Licence, string SourceVersion);

public sealed record NutritionImport(
    string Source,
    string SourceVersion,
    string Licence,
    string ImportedAt,
    int ProductCount,
    string Notes);
