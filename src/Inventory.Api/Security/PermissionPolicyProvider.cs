using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Security;

// Resuelve cualquier nombre de policy como un código de permiso — así
// [Authorize(Policy = Permisos.ProductosCrear)] funciona sin tener que registrar
// una AddPolicy por cada uno de los ~20 permisos en Program.cs.
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existente = await _fallback.GetPolicyAsync(policyName);
        if (existente is not null)
            return existente;

        var policy = new AuthorizationPolicyBuilder();
        policy.AddRequirements(new PermisoRequirement(policyName));
        return policy.Build();
    }
}
