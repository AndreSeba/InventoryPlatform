using Microsoft.AspNetCore.Authorization;

namespace Inventory.Api.Security;

public class PermisoRequirement : IAuthorizationRequirement
{
    public string Codigo { get; }

    public PermisoRequirement(string codigo) => Codigo = codigo;
}

public class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.HasClaim("permiso", requirement.Codigo))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
