using Microsoft.AspNetCore.Authorization;

namespace Inventory.Api.Security;

public class PermisoRequirement : IAuthorizationRequirement
{
    // Un solo código, o varios separados por «|» (alcanza con tener alguno).
    public string[] Codigos { get; }

    public PermisoRequirement(string codigo) =>
        Codigos = codigo.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class PermisoAuthorizationHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (requirement.Codigos.Any(c => context.User.HasClaim("permiso", c)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
