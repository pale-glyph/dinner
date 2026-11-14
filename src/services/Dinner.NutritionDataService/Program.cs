using Dinner.ServiceDefaults;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
var app = builder.Build().MapDinnerServiceDefaults("nutrition-data-service");
app.Run();
