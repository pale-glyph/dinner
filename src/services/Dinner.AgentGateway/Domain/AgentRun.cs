namespace Dinner.AgentGateway.Domain;

public sealed record AgentRun(
    Guid Id,
    Guid HouseholdId,
    string Status,
    Guid? ThreadId,
    int LastSequence,
    string[] Events,
    DateTimeOffset CreatedAt,
    string CorrelationId,
    bool HasPendingQueuedMessage = false)
{
    public static AgentRun Create(Guid householdId, string correlationId) => new(
        Guid.NewGuid(), householdId, AgentRunStatus.Starting, null, 0, [], DateTimeOffset.UtcNow, correlationId);

    public AgentRun AwaitPlatformConfiguration() => this with
    {
        Status = AgentRunStatus.AwaitingPlatformConfiguration,
        Events = ["Set AgentPlatform__BaseUrl, ProjectId, and AgentId to start a platform thread."],
    };

    public AgentRun Fail(string message) => this with { Status = AgentRunStatus.Failed, Events = [message] };

    public AgentRun Start(Guid threadId) => this with { Status = AgentRunStatus.Running, ThreadId = threadId };

    public AgentRun QueueMessage(string delivery) => this with
    {
        Status = AgentRunStatus.Running,
        HasPendingQueuedMessage = delivery == AgentMessageDelivery.Queued,
    };

    public AgentRun Advance(AgentTimelineEntry entry)
    {
        if (entry.SequenceNumber <= LastSequence)
        {
            return this;
        }

        return this with
        {
            LastSequence = entry.SequenceNumber,
            Status = entry.Role == AgentTimelineRole.Agent ? AgentRunStatus.ProposalReady : Status,
            HasPendingQueuedMessage = false,
            Events = entry.Content is null ? Events : [.. Events, entry.Content],
        };
    }
}

public static class AgentRunStatus
{
    public const string Starting = "Starting";
    public const string AwaitingPlatformConfiguration = "AwaitingPlatformConfiguration";
    public const string Running = "Running";
    public const string ProposalReady = "ProposalReady";
    public const string Failed = "Failed";
}

public static class AgentMessageDelivery
{
    public const string Queued = "Queued";
    public const string Steering = "Steering";
}

public sealed record AgentTimelineEntry(int SequenceNumber, string? Role, string? Content);

public static class AgentTimelineRole
{
    public const string Agent = "Agent";
}
