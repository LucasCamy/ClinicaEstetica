using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Security;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Identity;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler(
    EsteticaDbContext db,
    UserManager<ApplicationUser> userManager)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var userId))
        {
            return;
        }

        var user = await userManager.FindByIdAsync(idValue);
        if (user is null || !user.IsActive || user.MustChangePassword)
        {
            return;
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!user.TwoFactorEnabled && roles.Any(AppRoles.MfaRequired.Contains))
        {
            return;
        }

        var permissionOverride = await db.UserPermissionOverrides
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.Permission == requirement.Permission)
            .Select(item => (bool?)item.IsGranted)
            .SingleOrDefaultAsync();

        var granted = permissionOverride ?? RolePermissionCatalog.IsGrantedByDefault(roles, requirement.Permission);
        if (granted)
        {
            context.Succeed(requirement);
        }
    }
}
