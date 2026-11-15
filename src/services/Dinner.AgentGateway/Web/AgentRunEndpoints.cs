using System.Text;
using Dinner.AgentGateway.Application;
using Dinner.AgentGateway.Domain;

namespace Dinner.AgentGateway.Web;

public static class AgentRunEndpoints
{
    public static IEndpointRouteBuilder MapAgentRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var runs = endpoints.MapGroup("/api/v1/agent/runs").WithTags("Agent runs");
        runs.MapPost("", StartRunAsync);
        runs.MapGet("/{id:guid}", GetRunAsync);
        runs.MapGet("", ListRunsAsync);
        runs.MapDelete("/{id:guid}", DeleteRunAsync);
        runs.MapPost("/{id:guid}/messages", SendMessageAsync);
        runs.MapPost("/{id:guid}/reconcile", ReconcileAsync);
        endpoints.MapPost("/api/v1/agent/webhooks/events", ProcessWebhookAsync).WithTags("Agent webhooks");
        return endpoints;
    }

    private static async Task<IResult> StartRunAsync(StartRunRequest request, AgentRunService service, CancellationToken cancellationToken)
    {
        if (request.HouseholdId == Guid.Empty || string.IsNullOrWhiteSpace(request.Prompt)) return ValidationProblem("request", "householdId and prompt are required.");
        var result = await service.StartAsync(request.HouseholdId, request.Prompt, request.CorrelationId, cancellationToken);
        return result.Failure == PlatformFailure.None || result.Failure == PlatformFailure.NotConfigured
            ? Results.Accepted($"/api/v1/agent/runs/{result.Run.Id}", result.Run)
            : Results.Json(result.Run, statusCode: StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> GetRunAsync(Guid id, AgentRunService service, CancellationToken cancellationToken) => await service.GetAsync(id, cancellationToken) is { } run ? Results.Ok(run) : Results.NotFound();

    private static async Task<IResult> ListRunsAsync(Guid householdId, AgentRunService service, CancellationToken cancellationToken)
    {
        if (householdId == Guid.Empty) return ValidationProblem("householdId", "householdId is required.");
        return Results.Ok(await service.ListAsync(householdId, cancellationToken));
    }

    private static async Task<IResult> DeleteRunAsync(Guid id, AgentRunService service, CancellationToken cancellationToken)
    {
        var failure = await service.DeleteAsync(id, cancellationToken);
        return failure switch
        {
            PlatformFailure.None => Results.NoContent(),
            PlatformFailure.NotFound => Results.NotFound(),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway),
        };
    }

    private static async Task<IResult> SendMessageAsync(Guid id, RunMessageRequest request, AgentRunService service, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content)) return Results.Problem("Message content is required.", statusCode: StatusCodes.Status400BadRequest);
        var failure = await service.SendMessageAsync(id, request.Content, request.Delivery, cancellationToken);
        return failure switch
        {
            PlatformFailure.None => Results.Accepted(),
            PlatformFailure.NotFound => Results.NotFound(),
            PlatformFailure.ThreadNotStarted => Results.Conflict(new { code = "thread_not_started" }),
            PlatformFailure.QueuedMessagePending => Results.Conflict(new { code = "queued_message_pending" }),
            PlatformFailure.NotConfigured => Results.Problem("Agent Platform must be configured with an absolute HTTP(S) base URL and project ID.", statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway),
        };
    }

    private static async Task<IResult> ReconcileAsync(Guid id, AgentRunService service, CancellationToken cancellationToken)
    {
        var result = await service.ReconcileAsync(id, cancellationToken);
        return result.Failure switch
        {
            PlatformFailure.None => Results.Ok(result.Run),
            PlatformFailure.NotFound => Results.NotFound(),
            PlatformFailure.ThreadNotStarted => Results.Conflict(new { code = "thread_not_started" }),
            PlatformFailure.NotConfigured => Results.Problem("Agent Platform must be configured with an absolute HTTP(S) base URL and project ID.", statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.StatusCode(StatusCodes.Status502BadGateway),
        };
    }

    private static async Task<IResult> ProcessWebhookAsync(HttpRequest request, AgentRunService service, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, cancellationToken);
        var rawBody = buffer.ToArray();
        var valid = await service.ProcessWebhookAsync(rawBody, Encoding.UTF8.GetString(rawBody), request.Headers["X-Agent-Platform-Signature"].FirstOrDefault(), request.Headers["X-Agent-Platform-Event-Id"].FirstOrDefault(), cancellationToken);
        return valid ? Results.Ok() : Results.Unauthorized();
    }

    private static IResult ValidationProblem(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

public sealed record StartRunRequest(Guid HouseholdId, string? Prompt, string? CorrelationId);
public sealed record RunMessageRequest(string? Content, string? Delivery);
