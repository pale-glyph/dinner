using Dinner.AgentGateway.Application;
using Dinner.AgentGateway.Infrastructure;
using Dinner.AgentGateway.Web;
using Dinner.ServiceDefaults;
using Npgsql;

var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient(AgentPlatformHttpClient.ClientName);
builder.Services.Configure<AgentPlatformOptions>(builder.Configuration.GetSection(AgentPlatformOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Dinner")
    ?? "Host=localhost;Port=5432;Database=dinner;Username=dinner;Password=change-me";

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IAgentRunRepository, PostgresAgentRunRepository>();
builder.Services.AddSingleton<IWebhookEventRepository, PostgresWebhookEventRepository>();
builder.Services.AddSingleton<IAgentPlatformClient, AgentPlatformHttpClient>();
builder.Services.AddSingleton<IWebhookSignatureValidator, HmacWebhookSignatureValidator>();
builder.Services.AddSingleton<IWebhookPayloadParser, AgentPlatformWebhookParser>();
builder.Services.AddSingleton<AgentRunService>();

var app = builder.Build().MapDinnerServiceDefaults("agent-gateway");
app.UseCors();

await app.Services.GetRequiredService<IAgentRunRepository>().InitializeAsync();
app.MapAgentRunEndpoints();

app.Run();
