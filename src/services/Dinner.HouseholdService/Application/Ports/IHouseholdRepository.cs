using Dinner.HouseholdService.Domain;

namespace Dinner.HouseholdService.Application.Ports;

public interface IHouseholdRepository
{
    Task<AccountContext> GetOrCreateAccountAsync(string clerkUserId, string? email, string? displayName, CancellationToken cancellationToken);
    Task CreateAsync(Household household, Guid ownerId, CancellationToken cancellationToken);
    Task<Household?> GetAsync(Guid householdId, CancellationToken cancellationToken);
    Task UpdateAsync(Household household, CancellationToken cancellationToken);
    Task<IReadOnlyList<HouseholdMembership>> GetMembershipsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(Guid householdId, CancellationToken cancellationToken);
    Task<bool> IsOwnerAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken);
    Task<bool> RemoveMemberAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken);
    Task<string> CreateInviteAsync(Guid householdId, Guid createdByAccountId, CancellationToken cancellationToken);
    Task<HouseholdMembership?> JoinInviteAsync(string token, Guid accountId, CancellationToken cancellationToken);
}

public interface IHouseholdDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
