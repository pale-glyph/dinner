using Dinner.HouseholdService.Application.Ports;
using Dinner.HouseholdService.Application.Services;
using Dinner.HouseholdService.Infrastructure;
using Dinner.HouseholdService.Web;
using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var connectionString = builder.Configuration.GetConnectionString("Dinner") ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IHouseholdRepository, NpgsqlHouseholdRepository>();
builder.Services.AddSingleton<IHouseholdDatabaseInitializer, NpgsqlHouseholdRepository>();
builder.Services.AddScoped<HouseholdApplicationService>();
builder.Services.AddScoped<RequestAuthenticator>();

var app = builder.Build().MapDinnerServiceDefaults("household-service");
app.UseCors();
await app.Services.GetRequiredService<IHouseholdDatabaseInitializer>().InitializeAsync(CancellationToken.None);
app.MapHouseholdEndpoints();
app.Run();
