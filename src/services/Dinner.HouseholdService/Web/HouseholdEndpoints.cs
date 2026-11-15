using Dinner.HouseholdService.Application.Services;

namespace Dinner.HouseholdService.Web;

public static class HouseholdEndpoints
{
    public static void MapHouseholdEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/households").WithTags("Households");
        api.MapPost("", Create);
        api.MapGet("/mine", Mine);
        api.MapGet("/{id:guid}/members", Members);
        api.MapDelete("/{id:guid}/members/{accountId:guid}", RemoveMember);
        api.MapPost("/{id:guid}/invites", CreateInvite);
        api.MapPost("/invites/{token}/join", JoinInvite);
        app.MapGet("/api/v1/accounts/me", Account).WithTags("Accounts");
        app.MapGet("/mcp/resources/household/context", ContextResource);
        app.MapGet("/mcp/resources/household/preferences", PreferencesResource);
        app.MapGet("/mcp/resources/household/dietary-constraints", ConstraintsResource);
        app.MapPost("/mcp/tools/get_household_context", ContextTool);
        app.MapPost("/mcp/tools/get_household_preferences", PreferencesTool);
        app.MapPost("/mcp/tools/update_household_preferences", UpdatePreferences);
    }

    private static async Task<IResult> Create(HttpRequest request, HouseholdInput input, RequestAuthenticator authenticator, HouseholdApplicationService households, CancellationToken cancellationToken)
    {
        var account = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (account is null) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(input.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });
        var membership = await households.CreateAsync(new(input.Name, input.Servings, input.DietaryPreferences, input.Allergies, input.DislikedFoods), account.Id, cancellationToken);
        return Results.Created($"/api/v1/households/{membership.Id}", membership);
    }

    private static async Task<IResult> Mine(HttpRequest request, RequestAuthenticator authenticator, HouseholdApplicationService households, CancellationToken cancellationToken)
    {
        var account = await authenticator.AuthenticateAsync(request, cancellationToken);
        return account is null ? Results.Unauthorized() : Results.Ok(new { households = await households.GetMembershipsAsync(account.Id, cancellationToken) });
    }

    private static async Task<IResult> Members(Guid id, HttpRequest request, RequestAuthenticator authenticator, HouseholdApplicationService households, CancellationToken cancellationToken)
    {
        var account = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (account is null) return Results.Unauthorized();
        return !await households.IsOwnerAsync(id, account.Id, cancellationToken) ? Results.Forbid() : Results.Ok(new { members = await households.GetMembersAsync(id, cancellationToken) });
    }

    private static async Task<IResult> RemoveMember(Guid id, Guid accountId, HttpRequest request, RequestAuthenticator authenticator, HouseholdApplicationService households, CancellationToken cancellationToken)
    {
        var account = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (account is null) return Results.Unauthorized();
        if (!await households.IsOwnerAsync(id, account.Id, cancellationToken)) return Results.Forbid();
        if (accountId == account.Id) return Results.BadRequest(new { error = "The household owner cannot remove themself." });
        return await households.RemoveMemberAsync(id, accountId, cancellationToken) ? Results.NoContent() : Results.NotFound(new { error = "Household member not found." });
    }

    private static async Task<IResult> CreateInvite(Guid id, HttpRequest request, RequestAuthenticator authenticator, HouseholdApplicationService households, CancellationToken cancellationToken)
    {
        var account = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (account is null) return Results.Unauthorized();
        if (!await households.IsOwnerAsync(id, account.Id, cancellationToken)) return Results.Forbid();
        return Results.Ok(new { token = await households.CreateInviteAsync(id, account.Id, cancellationToken) });
    }

    private static async Task<IResult> JoinInvite(string token, HttpRequest request, RequestAuthenticator authenticator, HouseholdApplicationService households, CancellationToken cancellationToken)
    {
        var account = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (account is null) return Results.Unauthorized();
        var membership = await households.JoinInviteAsync(token, account.Id, cancellationToken);
        return membership is null ? Results.NotFound(new { error = "This invitation is no longer valid." }) : Results.Ok(membership);
    }

    private static async Task<IResult> Account(HttpRequest request, RequestAuthenticator authenticator, CancellationToken cancellationToken) =>
        await authenticator.AuthenticateAsync(request, cancellationToken) is { } account ? Results.Ok(account) : Results.Unauthorized();

    private static async Task<IResult> ContextResource(Guid householdId, HouseholdApplicationService households, CancellationToken cancellationToken) =>
        await households.GetAsync(householdId, cancellationToken) is { } household ? Results.Ok(new { uri = "household://context", household }) : Results.NotFound();
    private static async Task<IResult> PreferencesResource(Guid householdId, HouseholdApplicationService households, CancellationToken cancellationToken) =>
        await households.GetAsync(householdId, cancellationToken) is { } household ? Results.Ok(new { uri = "household://preferences", household.DietaryPreferences, household.DislikedFoods }) : Results.NotFound();
    private static async Task<IResult> ConstraintsResource(Guid householdId, HouseholdApplicationService households, CancellationToken cancellationToken) =>
        await households.GetAsync(householdId, cancellationToken) is { } household ? Results.Ok(new { uri = "household://dietary-constraints", household.Allergies }) : Results.NotFound();
    private static async Task<IResult> ContextTool(McpContext input, HouseholdApplicationService households, CancellationToken cancellationToken) =>
        await households.GetAsync(input.HouseholdId, cancellationToken) is { } household ? Results.Ok(new { structuredContent = household, summary = $"Household {household.Name} serves {household.Servings}." }) : Results.NotFound();
    private static async Task<IResult> PreferencesTool(McpContext input, HouseholdApplicationService households, CancellationToken cancellationToken) =>
        await households.GetAsync(input.HouseholdId, cancellationToken) is { } household ? Results.Ok(new { structuredContent = new { household.DietaryPreferences, household.Allergies, household.DislikedFoods } }) : Results.NotFound();
    private static async Task<IResult> UpdatePreferences(PreferencesInput input, HouseholdApplicationService households, CancellationToken cancellationToken) =>
        await households.UpdatePreferencesAsync(new(input.HouseholdId, input.DietaryPreferences, input.Allergies, input.DislikedFoods), cancellationToken) is { } household ? Results.Ok(new { structuredContent = household }) : Results.NotFound();
}

public sealed record HouseholdInput(string? Name, int? Servings, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods);
public sealed record McpContext(Guid HouseholdId, string? CorrelationId);
public sealed record PreferencesInput(Guid HouseholdId, string[]? DietaryPreferences, string[]? Allergies, string[]? DislikedFoods, string? CorrelationId);
