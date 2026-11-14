using Dinner.ServiceDefaults;
var builder = WebApplication.CreateBuilder(args).AddDinnerServiceDefaults();
var app = builder.Build().MapDinnerServiceDefaults("household-service");
app.Run();
