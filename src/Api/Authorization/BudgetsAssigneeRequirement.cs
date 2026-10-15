using Microsoft.AspNetCore.Authorization;
using SnG.Control.Application.Identity;

namespace SnG.Control.Api.Authorization;

public sealed class BudgetsAssigneeRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "BudgetsAssignee";
}

public sealed class BudgetsAssigneeAuthorizationHandler(IBudgetsAssigneeIdentity budgets)
    : AuthorizationHandler<BudgetsAssigneeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BudgetsAssigneeRequirement requirement)
    {
        var subject = context.User.FindFirst("sub")?.Value;
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(subject)
            && string.Equals(subject, budgets.Subject, StringComparison.Ordinal))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
