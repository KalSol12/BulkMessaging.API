using System.Security.Claims;
using BulkMessaging.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BulkMessaging.API.Authorization;

public sealed class PasswordChangedRequirement : IAuthorizationRequirement
{
}

public sealed class PasswordChangedAuthorizationHandler
    : AuthorizationHandler<PasswordChangedRequirement>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PasswordChangedAuthorizationHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PasswordChangedRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null
            ? null
            : await _userManager.FindByIdAsync(userId);

        if (user is not null && !user.MustChangePassword)
        {
            context.Succeed(requirement);
        }
    }
}
