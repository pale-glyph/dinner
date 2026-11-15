using Dinner.HouseholdService.Application.Services;
using Dinner.HouseholdService.Domain;

namespace Dinner.HouseholdService.Web;

public sealed class RequestAuthenticator(IConfiguration configuration, HouseholdApplicationService households)
{
    public async Task<AccountContext?> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var expectedKey = configuration["Dinner:InternalApiKey"];
        var providedKey = request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        var clerkUserId = request.Headers["X-Clerk-User-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(expectedKey, providedKey, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(clerkUserId)) return null;
        return await households.GetOrCreateAccountAsync(clerkUserId, request.Headers["X-Account-Email"].FirstOrDefault(), request.Headers["X-Account-Name"].FirstOrDefault(), cancellationToken);
    }
}
