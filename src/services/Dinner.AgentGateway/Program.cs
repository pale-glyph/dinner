using Dinner.ServiceDefaults;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
var app = builder.Build().MapDinnerServiceDefaults("agent-gateway");
app.MapPost("/api/v1/agent/webhooks/events", () => Results.Accepted()).WithTags("Agent webhooks");
app.Run();
