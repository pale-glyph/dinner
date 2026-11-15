using Dinner.HouseholdService.Application.Ports;
using Dinner.HouseholdService.Domain;

namespace Dinner.HouseholdService.Application.Services;

public sealed class HouseholdApplicationService(IHouseholdRepository households)
{
    public Task<AccountContext> GetOrCreateAccountAsync(string clerkUserId, string? email, string? displayName, CancellationToken cancellationToken) => households.GetOrCreateAccountAsync(clerkUserId, email, displayName, cancellationToken);
    public async Task<HouseholdMembership> CreateAsync(CreateHouseholdCommand command, Guid ownerId, CancellationToken cancellationToken)
    {
        var household = new Household(Guid.NewGuid(), command.Name.Trim(), command.Servings is > 0 ? command.Servings.Value : 2, command.DietaryPreferences ?? [], command.Allergies ?? [], command.DislikedFoods ?? []);
        await households.CreateAsync(household, ownerId, cancellationToken);
        return new HouseholdMembership(household.Id, household.Name, "owner");
    }

    public Task<IReadOnlyList<HouseholdMembership>> GetMembershipsAsync(Guid accountId, CancellationToken cancellationToken) => households.GetMembershipsAsync(accountId, cancellationToken);
    public Task<IReadOnlyList<HouseholdMember>> GetMembersAsync(Guid householdId, CancellationToken cancellationToken) => households.GetMembersAsync(householdId, cancellationToken);
    public Task<bool> IsOwnerAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken) => households.IsOwnerAsync(householdId, accountId, cancellationToken);
    public Task<bool> RemoveMemberAsync(Guid householdId, Guid accountId, CancellationToken cancellationToken) => households.RemoveMemberAsync(householdId, accountId, cancellationToken);
    public Task<string> CreateInviteAsync(Guid householdId, Guid creatorId, CancellationToken cancellationToken) => households.CreateInviteAsync(householdId, creatorId, cancellationToken);
    public Task<HouseholdMembership?> JoinInviteAsync(string token, Guid accountId, CancellationToken cancellationToken) => households.JoinInviteAsync(token, accountId, cancellationToken);
    public Task<Household?> GetAsync(Guid householdId, CancellationToken cancellationToken) => households.GetAsync(householdId, cancellationToken);
    public async Task<Household?> UpdatePreferencesAsync(UpdatePreferencesCommand command, CancellationToken cancellationToken)
    {
        var household = await households.GetAsync(command.HouseholdId, cancellationToken);
        if (household is null) return null;
        var updated = household.UpdatePreferences(command.DietaryPreferences, command.Allergies, command.DislikedFoods);
        await households.UpdateAsync(updated, cancellationToken);
        return updated;
    }
}

public sealed record CreateHouseholdCommand(string Name, int? Servings, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods);
public sealed record UpdatePreferencesCommand(Guid HouseholdId, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods);
