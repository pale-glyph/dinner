using Dinner.RecipeService.Application;
using Dinner.RecipeService.Application.Ports;
using Dinner.RecipeService.Infrastructure;
using Dinner.RecipeService.Web;
using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var connectionString = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<NpgsqlRecipeRepository>();
builder.Services.AddSingleton<IRecipeRepository>(provider => provider.GetRequiredService<NpgsqlRecipeRepository>());
builder.Services.AddSingleton<IRecipeDatabaseInitializer>(provider => provider.GetRequiredService<NpgsqlRecipeRepository>());
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<RecipeApplicationService>();

var app = builder.Build().MapDinnerServiceDefaults("recipe-service");
app.UseCors();
await app.Services.GetRequiredService<IRecipeDatabaseInitializer>().InitializeAsync(CancellationToken.None);
app.MapRecipeEndpoints();
app.Run();
