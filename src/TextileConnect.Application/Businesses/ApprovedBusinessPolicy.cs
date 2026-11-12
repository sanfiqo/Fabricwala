using Microsoft.AspNetCore.Authorization;

namespace TextileConnect.Application.Businesses;

public sealed class ApprovedBusinessRequirement : IAuthorizationRequirement;

public sealed class ApprovedBusinessHandler : AuthorizationHandler<ApprovedBusinessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApprovedBusinessRequirement requirement)
    {
        if (context.User.HasClaim("business_approved", "true"))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
