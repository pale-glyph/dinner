using Dinner.AgentGateway.Domain;

namespace Dinner.AgentGateway.Application;

public sealed class AgentRunService(
    IAgentRunRepository runs,
    IWebhookEventRepository webhookEvents,
    IAgentPlatformClient platform,
    IWebhookSignatureValidator signatureValidator,
    IWebhookPayloadParser webhookPayloadParser,
    IConfiguration configuration)
{
    public async Task<StartRunResult> StartAsync(Guid householdId, string prompt, string? correlationId, CancellationToken cancellationToken)
    {
        var run = AgentRun.Create(householdId, correlationId ?? Guid.NewGuid().ToString());
        await runs.SaveAsync(run, cancellationToken);
        var response = await platform.CreateThreadAsync(prompt, cancellationToken);
        if (response.Failure == PlatformFailure.NotConfigured)
        {
            run = run.AwaitPlatformConfiguration();
        }
        else if (!response.IsSuccess)
        {
            run = run.Fail(response.Failure == PlatformFailure.Unavailable
                ? "The configured agent platform could not be reached. Check AGENT_PLATFORM_BASE_URL and that the platform is reachable from the agent-gateway container."
                : "The agent platform rejected thread creation.");
        }
        else
        {
            run = run.Start(response.Value!);
        }

        await runs.SaveAsync(run, cancellationToken);
        return new StartRunResult(run, response.Failure);
    }

    public Task<AgentRun?> GetAsync(Guid id, CancellationToken cancellationToken) => runs.GetAsync(id, cancellationToken);
    public Task<IReadOnlyList<AgentRun>> ListAsync(Guid householdId, CancellationToken cancellationToken) => runs.ListAsync(householdId, cancellationToken);

    public async Task<PlatformFailure> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        if (run is null) return PlatformFailure.NotFound;
        if (run.ThreadId is not null)
        {
            var response = await platform.DeleteThreadAsync(run.ThreadId.Value, cancellationToken);
            if (!response.IsSuccess && response.Failure is not PlatformFailure.NotFound and not PlatformFailure.NotConfigured) return response.Failure;
        }

        await runs.DeleteAsync(id, cancellationToken);
        return PlatformFailure.None;
    }

    public async Task<PlatformFailure> SendMessageAsync(Guid id, string content, string? requestedDelivery, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        if (run is null) return PlatformFailure.NotFound;
        if (run.ThreadId is null) return PlatformFailure.ThreadNotStarted;
        if (run.HasPendingQueuedMessage && requestedDelivery != AgentMessageDelivery.Steering) return PlatformFailure.QueuedMessagePending;
        var delivery = requestedDelivery == AgentMessageDelivery.Steering ? AgentMessageDelivery.Steering : AgentMessageDelivery.Queued;
        var response = await platform.SendMessageAsync(run.ThreadId.Value, content, delivery, cancellationToken);
        if (!response.IsSuccess) return response.Failure;
        await runs.SaveAsync(run.QueueMessage(delivery), cancellationToken);
        return PlatformFailure.None;
    }

    public async Task<ReconcileResult> ReconcileAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(id, cancellationToken);
        if (run is null) return new ReconcileResult(null, PlatformFailure.NotFound);
        if (run.ThreadId is null) return new ReconcileResult(run, PlatformFailure.ThreadNotStarted);
        var response = await platform.GetTimelineAsync(run.ThreadId.Value, run.LastSequence, cancellationToken);
        if (!response.IsSuccess) return new ReconcileResult(run, response.Failure);
        foreach (var entry in response.Value!) run = run.Advance(entry);
        await runs.SaveAsync(run, cancellationToken);
        return new ReconcileResult(run);
    }

    public async Task<bool> ProcessWebhookAsync(byte[] rawBody, string payload, string? signature, string? eventId, CancellationToken cancellationToken)
    {
        var secret = configuration["AgentPlatform:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature) || !signatureValidator.IsValid(rawBody, secret, signature)) return false;
        if (string.IsNullOrWhiteSpace(eventId) || !await webhookEvents.TryRecordAsync(eventId, payload, cancellationToken)) return true;
        var eventEntry = webhookPayloadParser.Parse(payload);
        if (eventEntry is null) return true;
        var run = await runs.GetByThreadIdAsync(eventEntry.ThreadId, cancellationToken);
        if (run is not null) await runs.SaveAsync(run.Advance(eventEntry.Entry), cancellationToken);
        return true;
    }
}

public sealed record StartRunResult(AgentRun Run, PlatformFailure Failure = PlatformFailure.None);
public sealed record ReconcileResult(AgentRun? Run, PlatformFailure Failure = PlatformFailure.None);
