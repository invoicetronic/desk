using Microsoft.AspNetCore.Authorization;

namespace Desk;

/// <summary>
/// Custom authorization requirement checked at runtime.
/// In standalone mode, always succeeds (no auth needed).
/// In multi-user mode, requires an authenticated user.
/// </summary>
public class DeskAuthRequirement : IAuthorizationRequirement;

public class DeskAuthHandler(DeskConfig config) : AuthorizationHandler<DeskAuthRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, DeskAuthRequirement requirement)
    {
        if (config.IsStandalone || context.User.Identity?.IsAuthenticated == true)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
