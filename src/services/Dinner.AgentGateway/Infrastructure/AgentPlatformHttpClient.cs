using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dinner.AgentGateway.Application;
using Dinner.AgentGateway.Domain;
using Microsoft.Extensions.Options;

namespace Dinner.AgentGateway.Infrastructure;

public sealed class AgentPlatformOptions
{
    public const string SectionName = "AgentPlatform";
    public string? BaseUrl { get; init; }
    public string? ProjectId { get; init; }
    public string? AgentId { get; init; }
    public string? WebhookSecret { get; init; }
}

public sealed class AgentPlatformWebhookSecretProvider(IOptions<AgentPlatformOptions> options)
    : IAgentPlatformWebhookSecretProvider
{
    public string? GetWebhookSecret() => options.Value.WebhookSecret;
}

public sealed class AgentPlatformHttpClient(IHttpClientFactory clientFactory, IOptions<AgentPlatformOptions> options) : IAgentPlatformClient
{
    public const string ClientName = "agent-platform";

    public async Task<PlatformResponse<Guid>> CreateThreadAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!TryGetConfiguration(true, out var baseUri, out var projectId, out var agentId)) return PlatformResponse<Guid>.Failed(PlatformFailure.NotConfigured);
        try
        {
            using var response = await clientFactory.CreateClient(ClientName).PostAsJsonAsync($"{baseUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agents/{agentId}/threads", new { initialPrompt = prompt }, cancellationToken);
            if (!response.IsSuccessStatusCode) return PlatformResponse<Guid>.Failed(PlatformFailure.Rejected);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("id", out var id) && id.TryGetGuid(out var threadId)
                ? PlatformResponse<Guid>.Success(threadId)
                : PlatformResponse<Guid>.Failed(PlatformFailure.Rejected);
        }
        catch (HttpRequestException)
        {
            return PlatformResponse<Guid>.Failed(PlatformFailure.Unavailable);
        }
    }

    public Task<PlatformResponse> DeleteThreadAsync(Guid threadId, CancellationToken cancellationToken = default) => SendAsync(HttpMethod.Delete, threadId, null, cancellationToken);
    public Task<PlatformResponse> SendMessageAsync(Guid threadId, string content, string delivery, CancellationToken cancellationToken = default) => SendAsync(HttpMethod.Post, threadId, new { content, delivery }, cancellationToken);

    public async Task<PlatformResponse<IReadOnlyList<AgentTimelineEntry>>> GetTimelineAsync(Guid threadId, int afterSequence, CancellationToken cancellationToken = default)
    {
        if (!TryGetConfiguration(false, out var baseUri, out var projectId, out _)) return PlatformResponse<IReadOnlyList<AgentTimelineEntry>>.Failed(PlatformFailure.NotConfigured);
        try
        {
            using var response = await clientFactory.CreateClient(ClientName).GetAsync($"{baseUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agent-threads/{threadId}/timeline?afterSequence={afterSequence}&limit=100", cancellationToken);
            if (!response.IsSuccessStatusCode) return PlatformResponse<IReadOnlyList<AgentTimelineEntry>>.Failed(ToFailure(response.StatusCode));
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var entries = document.RootElement.GetProperty("items").EnumerateArray().Select(ParseTimelineEntry).ToArray();
            return PlatformResponse<IReadOnlyList<AgentTimelineEntry>>.Success(entries);
        }
        catch (HttpRequestException)
        {
            return PlatformResponse<IReadOnlyList<AgentTimelineEntry>>.Failed(PlatformFailure.Unavailable);
        }
    }

    private async Task<PlatformResponse> SendAsync(HttpMethod method, Guid threadId, object? body, CancellationToken cancellationToken)
    {
        if (!TryGetConfiguration(false, out var baseUri, out var projectId, out _)) return PlatformResponse.Failed(PlatformFailure.NotConfigured);
        try
        {
            var path = $"{baseUri.AbsoluteUri.TrimEnd('/')}/api/projects/{projectId}/agent-threads/{threadId}";
            using var request = new HttpRequestMessage(method, body is null ? path : $"{path}/messages") { Content = body is null ? null : JsonContent.Create(body) };
            using var response = await clientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? PlatformResponse.Success() : PlatformResponse.Failed(ToFailure(response.StatusCode));
        }
        catch (HttpRequestException)
        {
            return PlatformResponse.Failed(PlatformFailure.Unavailable);
        }
    }

    private bool TryGetConfiguration(bool requireAgentId, out Uri baseUri, out string projectId, out string? agentId)
    {
        var configuration = options.Value;
        var validUri = Uri.TryCreate(configuration.BaseUrl, UriKind.Absolute, out baseUri!)
            && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps);
        projectId = configuration.ProjectId ?? string.Empty;
        agentId = configuration.AgentId;
        return validUri && !string.IsNullOrWhiteSpace(projectId) && (!requireAgentId || !string.IsNullOrWhiteSpace(agentId));
    }

    private static PlatformFailure ToFailure(HttpStatusCode statusCode) => statusCode == HttpStatusCode.NotFound ? PlatformFailure.NotFound : PlatformFailure.Rejected;

    internal static AgentTimelineEntry ParseTimelineEntry(JsonElement entry)
    {
        var sequence = entry.TryGetProperty("sequenceNumber", out var sequenceNode) && sequenceNode.TryGetInt32(out var value) ? value : 0;
        var role = entry.TryGetProperty("role", out var roleNode) && roleNode.ValueKind == JsonValueKind.String ? roleNode.GetString() : null;
        var content = entry.TryGetProperty("content", out var contentNode) && contentNode.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? contentNode.ValueKind == JsonValueKind.String ? contentNode.GetString() : contentNode.GetRawText() : null;
        return new AgentTimelineEntry(sequence, role, content);
    }
}

public sealed class AgentPlatformWebhookParser : IWebhookPayloadParser
{
    public WebhookTimelineEntry? Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("threadId", out var threadIdNode) || !threadIdNode.TryGetGuid(out var threadId)) return null;
        return new WebhookTimelineEntry(threadId, AgentPlatformHttpClient.ParseTimelineEntry(data));
    }
}
