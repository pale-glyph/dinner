using Dinner.NutritionDataService.Application;
using Dinner.NutritionDataService.Infrastructure;
using Dinner.NutritionDataService.Web;
using Dinner.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<INutritionProductCatalog, FixtureNutritionProductCatalog>();
builder.Services.AddSingleton<NutritionCatalogService>();

var app = builder.Build().MapDinnerServiceDefaults("nutrition-data-service");
app.UseCors();
app.MapNutritionEndpoints();

app.Run();
