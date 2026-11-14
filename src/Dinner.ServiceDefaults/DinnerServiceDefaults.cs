using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dinner.ServiceDefaults;

public static class DinnerServiceDefaults
{
    public static WebApplicationBuilder AddDinnerServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks();
        builder.Services.AddProblemDetails();
        return builder;
    }

    public static WebApplication MapDinnerServiceDefaults(this WebApplication app, string serviceName)
    {
        app.UseExceptionHandler();
        app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? Guid.NewGuid().ToString("N");

            context.Response.Headers["X-Correlation-ID"] = correlationId;
            using (app.Logger.BeginScope(new Dictionary<string, object?>
                   {
                       ["CorrelationId"] = correlationId,
                       ["Service"] = serviceName
                   }))
            {
                await next();
            }
        });

        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = serviceName }))
            .WithName("Health")
            .WithTags("Operations");
        app.MapHealthChecks("/health/ready");
        app.MapGet("/", () => Results.Ok(new { service = serviceName, status = "skeleton" }))
            .ExcludeFromDescription();
        return app;
    }
}
