using Dinner.PantryService.Application.Ports;
using Dinner.PantryService.Application.Services;
using Dinner.PantryService.Infrastructure;
using Dinner.PantryService.Web;
using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var connectionString = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<NpgsqlPantryRepository>();
builder.Services.AddSingleton<IPantryRepository>(provider => provider.GetRequiredService<NpgsqlPantryRepository>());
builder.Services.AddSingleton<IPantryDatabaseInitializer>(provider => provider.GetRequiredService<NpgsqlPantryRepository>());
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<PantryApplicationService>();

var app = builder.Build().MapDinnerServiceDefaults("pantry-service");
app.UseCors();
await app.Services.GetRequiredService<IPantryDatabaseInitializer>().InitializeAsync(CancellationToken.None);
app.MapPantryEndpoints();
app.Run();
