using Dinner.MealPlanningService.Application;
using Dinner.MealPlanningService.Infrastructure;
using Dinner.MealPlanningService.Web;
using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var connectionString = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IMealPlanRepository, PostgresMealPlanRepository>();
builder.Services.AddSingleton<MealPlanService>();
var app = builder.Build().MapDinnerServiceDefaults("meal-planning-service");
app.UseCors();
await app.Services.GetRequiredService<IMealPlanRepository>().InitializeAsync();
app.MapMealPlanEndpoints();
app.Run();
