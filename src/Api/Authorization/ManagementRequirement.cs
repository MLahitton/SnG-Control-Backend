using Microsoft.AspNetCore.Authorization;
using SnG.Control.Application.Identity;

namespace SnG.Control.Api.Authorization;

public sealed class ManagementRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "Management";
}

public sealed class ManagementAuthorizationHandler(IManagementIdentity management)
    : AuthorizationHandler<ManagementRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ManagementRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(subject)
            && string.Equals(subject, management.Subject, StringComparison.Ordinal))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
