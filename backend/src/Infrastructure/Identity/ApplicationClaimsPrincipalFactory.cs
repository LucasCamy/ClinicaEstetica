using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PainelEstetica.Application.Security;

namespace PainelEstetica.Infrastructure.Identity;

public sealed class ApplicationClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(
        userManager,
        roleManager,
        optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(IdentityClaimTypes.MustChangePassword, "true"));
        }

        var roles = await UserManager.GetRolesAsync(user);
        if (!user.TwoFactorEnabled && roles.Any(AppRoles.MfaRequired.Contains))
        {
            identity.AddClaim(new Claim(IdentityClaimTypes.MfaSetupRequired, "true"));
        }

        return identity;
    }
}
