using Dinner.ServiceDefaults;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
var app = builder.Build().MapDinnerServiceDefaults("pantry-service");
app.Run();
