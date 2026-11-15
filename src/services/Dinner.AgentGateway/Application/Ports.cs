using Dinner.AgentGateway.Domain;

namespace Dinner.AgentGateway.Application;

public interface IAgentRunRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<AgentRun?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AgentRun?> GetByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentRun>> ListAsync(Guid householdId, CancellationToken cancellationToken = default);
    Task SaveAsync(AgentRun run, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IWebhookEventRepository
{
    Task<bool> TryRecordAsync(string eventId, string payload, CancellationToken cancellationToken = default);
}

public interface IWebhookSignatureValidator
{
    bool IsValid(byte[] body, string secret, string signature);
}

public interface IWebhookPayloadParser
{
    WebhookTimelineEntry? Parse(string payload);
}

public interface IAgentPlatformClient
{
    Task<PlatformResponse<Guid>> CreateThreadAsync(string prompt, CancellationToken cancellationToken = default);
    Task<PlatformResponse> DeleteThreadAsync(Guid threadId, CancellationToken cancellationToken = default);
    Task<PlatformResponse> SendMessageAsync(Guid threadId, string content, string delivery, CancellationToken cancellationToken = default);
    Task<PlatformResponse<IReadOnlyList<AgentTimelineEntry>>> GetTimelineAsync(Guid threadId, int afterSequence, CancellationToken cancellationToken = default);
}

public sealed record PlatformResponse(bool IsSuccess, PlatformFailure Failure = PlatformFailure.None)
{
    public static PlatformResponse Success() => new(true);
    public static PlatformResponse Failed(PlatformFailure failure) => new(false, failure);
}

public sealed record PlatformResponse<T>(bool IsSuccess, T? Value = default, PlatformFailure Failure = PlatformFailure.None)
{
    public static PlatformResponse<T> Success(T value) => new(true, value);
    public static PlatformResponse<T> Failed(PlatformFailure failure) => new(false, default, failure);
}

public enum PlatformFailure
{
    None,
    NotConfigured,
    Unavailable,
    Rejected,
    NotFound,
    ThreadNotStarted,
    QueuedMessagePending,
}

public sealed record WebhookTimelineEntry(Guid ThreadId, AgentTimelineEntry Entry);
