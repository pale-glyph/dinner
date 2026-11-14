using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

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

    /// <summary>
    /// Exposes the service's existing validated /mcp/tools/* handlers through the
    /// standard MCP Streamable HTTP JSON-RPC transport.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpHttpServer(this IEndpointRouteBuilder endpoints, McpServerDefinition definition)
    {
        endpoints.MapPost("/mcp", async (HttpRequest request, CancellationToken cancellationToken) =>
        {
            JsonDocument document;
            try { document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken); }
            catch (JsonException) { return RpcError(null, -32700, "Parse error"); }

            using (document)
            {
                var root = document.RootElement;
                var id = root.TryGetProperty("id", out var idNode) ? idNode.Clone() : (JsonElement?)null;
                if (!root.TryGetProperty("method", out var methodNode) || methodNode.ValueKind != JsonValueKind.String)
                    return RpcError(id, -32600, "Invalid Request");

                var method = methodNode.GetString();
                var parameters = root.TryGetProperty("params", out var parameterNode) ? parameterNode : default;
                if (method == "notifications/initialized") return Results.Accepted();

                if (method == "initialize")
                {
                    var version = parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("protocolVersion", out var versionNode)
                        ? versionNode.GetString() ?? "2025-03-26" : "2025-03-26";
                    return RpcResult(id, new
                    {
                        protocolVersion = version,
                        capabilities = new { tools = new { }, resources = new { listChanged = false } },
                        serverInfo = new { name = definition.Name, version = "1.0.0" }
                    });
                }

                if (method == "tools/list")
                    return RpcResult(id, new
                    {
                        tools = definition.Tools.Select(name =>
                        {
                            var detail = definition.ToolDefinitions?.GetValueOrDefault(name);
                            return new
                            {
                                name,
                                description = detail?.Description ?? $"Dinner {definition.Name} capability: {name}.",
                                inputSchema = detail?.InputSchema ?? new { type = "object", additionalProperties = true }
                            };
                        })
                    });

                if (method == "resources/list")
                    return RpcResult(id, new { resources = definition.Resources.Select(uri => new { uri, name = uri, mimeType = "application/json" }) });

                if (method == "tools/call")
                {
                    if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("name", out var toolNode) || toolNode.ValueKind != JsonValueKind.String)
                        return RpcError(id, -32602, "Tool name is required");
                    var tool = toolNode.GetString()!;
                    if (!definition.Tools.Contains(tool, StringComparer.Ordinal)) return RpcError(id, -32602, $"Unknown tool: {tool}");
                    var arguments = parameters.TryGetProperty("arguments", out var argumentNode) ? argumentNode.GetRawText() : "{}";
                    using var client = new HttpClient { BaseAddress = new Uri($"{request.Scheme}://127.0.0.1:{request.Host.Port ?? 8080}") };
                    using var response = await client.PostAsync($"/mcp/tools/{tool}", new StringContent(arguments, Encoding.UTF8, "application/json"), cancellationToken);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!response.IsSuccessStatusCode)
                        return RpcResult(id, new { content = new[] { new { type = "text", text = body } }, isError = true });
                    using var result = JsonDocument.Parse(body);
                    var structuredContent = result.RootElement.TryGetProperty("structuredContent", out var structured) ? structured.Clone() : result.RootElement.Clone();
                    return RpcResult(id, new { content = new[] { new { type = "text", text = body } }, structuredContent });
                }

                return RpcError(id, -32601, $"Method not found: {method}");
            }
        }).WithTags("MCP");

        return endpoints;
    }

    private static IResult RpcResult(JsonElement? id, object result) => Results.Json(new { jsonrpc = "2.0", id = id ?? default(JsonElement), result });
    private static IResult RpcError(JsonElement? id, int code, string message) => Results.Json(new { jsonrpc = "2.0", id = id ?? default(JsonElement), error = new { code, message } });
}

public sealed record McpServerDefinition(string Name, string[] Tools, string[] Resources, IReadOnlyDictionary<string, McpToolDefinition>? ToolDefinitions = null);
public sealed record McpToolDefinition(string Description, object InputSchema);
