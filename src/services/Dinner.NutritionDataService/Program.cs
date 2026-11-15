using Dinner.NutritionDataService.Application;
using Dinner.NutritionDataService.Application.Ports;
using Dinner.NutritionDataService.Infrastructure;
using Dinner.NutritionDataService.Web;
using Dinner.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSingleton<FixtureNutritionProductCatalog>();
builder.Services.AddSingleton<INutritionProductCatalog>(provider => provider.GetRequiredService<FixtureNutritionProductCatalog>());
builder.Services.AddScoped<NutritionCatalogService>();

var app = builder.Build().MapDinnerServiceDefaults("nutrition-data-service");
app.UseCors();
app.MapNutritionEndpoints();

app.Run();
