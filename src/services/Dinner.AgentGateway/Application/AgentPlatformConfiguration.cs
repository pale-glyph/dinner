namespace Dinner.AgentGateway.Application;

public interface IAgentPlatformWebhookSecretProvider
{
    string? GetWebhookSecret();
}
