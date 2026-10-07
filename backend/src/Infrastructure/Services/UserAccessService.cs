using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Identity;
using PainelEstetica.Application.Security;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Identity;

namespace PainelEstetica.Infrastructure.Services;

public sealed class UserAccessService(
    EsteticaDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IClock clock) : IUserAccessService
{
    public async Task<PagedResult<UserAdminDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim().ToLower();
            query = query.Where(user =>
                (user.UserName != null && user.UserName.ToLower().Contains(search)) ||
                (user.Email != null && user.Email.ToLower().Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .Include(user => user.PermissionOverrides)
            .OrderBy(user => user.UserName)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserAdminDto>(users.Count);
        foreach (var user in users)
        {
            items.Add(await ToDtoAsync(user));
        }

        return new PagedResult<UserAdminDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<UserAdminDto> CreateAsync(
        CreateUserRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var roles = NormalizeAndValidateRoles(request.Roles);
        await EnsureRolesExistAsync(roles);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Username.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true,
            CreatedAtUtc = clock.UtcNow,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        EnsureIdentitySucceeded(await userManager.CreateAsync(user, request.TemporaryPassword));
        EnsureIdentitySucceeded(await userManager.AddToRolesAsync(user, roles));
        return await ToDtoAsync(user);
    }

    public async Task<UserAdminDto> UpdateAccessAsync(
        Guid id,
        UpdateUserAccessRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.Users
                       .Include(item => item.PermissionOverrides)
                       .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                   ?? throw new ResourceNotFoundException("Usuário", id);
        var roles = NormalizeAndValidateRoles(request.Roles);
        await EnsureRolesExistAsync(roles);
        await EnsureLastAdminIsPreservedAsync(user, roles.Contains(AppRoles.Admin));

        var currentRoles = await userManager.GetRolesAsync(user);
        EnsureIdentitySucceeded(await userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles)));
        EnsureIdentitySucceeded(await userManager.AddToRolesAsync(user, roles.Except(currentRoles)));

        var duplicateOverride = request.PermissionOverrides
            .GroupBy(item => item.Permission, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOverride is not null)
        {
            throw new BusinessRuleException($"A permissão {duplicateOverride.Key} foi informada mais de uma vez.");
        }

        var invalidPermission = request.PermissionOverrides
            .Select(item => item.Permission)
            .FirstOrDefault(permission => !AppPermissions.All.Contains(permission, StringComparer.Ordinal));
        if (invalidPermission is not null)
        {
            throw new BusinessRuleException($"Permissão desconhecida: {invalidPermission}.");
        }

        db.UserPermissionOverrides.RemoveRange(user.PermissionOverrides);
        foreach (var permission in request.PermissionOverrides)
        {
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = user.Id,
                Permission = permission.Permission,
                IsGranted = permission.IsGranted,
                UpdatedAtUtc = clock.UtcNow,
                UpdatedByUserId = actorUserId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        EnsureIdentitySucceeded(await userManager.UpdateSecurityStampAsync(user));
        return await ToDtoAsync(user);
    }

    public async Task<UserAdminDto> UpdateStatusAsync(
        Guid id,
        bool isActive,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
                   ?? throw new ResourceNotFoundException("Usuário", id);
        if (!isActive && id == actorUserId)
        {
            throw new BusinessRuleException("Você não pode desativar a própria conta.");
        }

        if (!isActive)
        {
            await EnsureLastAdminIsPreservedAsync(user, willRemainAdmin: false);
        }

        user.IsActive = isActive;
        if (!isActive)
        {
            user.LockoutEnd = DateTimeOffset.MaxValue;
        }
        else
        {
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }

        EnsureIdentitySucceeded(await userManager.UpdateAsync(user));
        EnsureIdentitySucceeded(await userManager.UpdateSecurityStampAsync(user));
        return await ToDtoAsync(user);
    }

    public async Task<ResetPasswordDto> ResetPasswordAsync(
        Guid id,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString())
                   ?? throw new ResourceNotFoundException("Usuário", id);
        if (!user.IsActive)
        {
            throw new BusinessRuleException("Ative o usuário antes de redefinir sua senha.");
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        EnsureIdentitySucceeded(await userManager.ResetPasswordAsync(user, token, temporaryPassword));
        user.MustChangePassword = true;
        EnsureIdentitySucceeded(await userManager.UpdateAsync(user));
        EnsureIdentitySucceeded(await userManager.UpdateSecurityStampAsync(user));
        return new ResetPasswordDto(temporaryPassword);
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new ResourceNotFoundException("Usuário", userId);
        var roles = await userManager.GetRolesAsync(user);
        var permissions = RolePermissionCatalog.ForRoles(roles).ToHashSet(StringComparer.Ordinal);
        var overrides = await db.UserPermissionOverrides
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var item in overrides)
        {
            if (item.IsGranted) permissions.Add(item.Permission);
            else permissions.Remove(item.Permission);
        }

        return permissions.Order(StringComparer.Ordinal).ToArray();
    }

    private async Task<UserAdminDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var overrides = await db.UserPermissionOverrides
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.Permission)
            .Select(item => new UserPermissionOverrideDto(item.Permission, item.IsGranted))
            .ToListAsync();
        var effective = await GetEffectivePermissionsAsync(user.Id, CancellationToken.None);
        return new UserAdminDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.IsActive,
            user.MustChangePassword,
            user.TwoFactorEnabled,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            roles.Order(StringComparer.Ordinal).ToArray(),
            overrides,
            effective);
    }

    private static string[] NormalizeAndValidateRoles(IEnumerable<string> requestedRoles)
    {
        var roles = requestedRoles
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (roles.Length == 0)
        {
            throw new BusinessRuleException("Informe ao menos um papel para o usuário.");
        }

        var invalid = roles.FirstOrDefault(role => !AppRoles.All.Contains(role, StringComparer.Ordinal));
        if (invalid is not null)
        {
            throw new BusinessRuleException($"Papel desconhecido: {invalid}.");
        }

        return roles;
    }

    private async Task EnsureRolesExistAsync(IEnumerable<string> roles)
    {
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureIdentitySucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)));
            }
        }
    }

    private async Task EnsureLastAdminIsPreservedAsync(ApplicationUser user, bool willRemainAdmin)
    {
        if (willRemainAdmin || !await userManager.IsInRoleAsync(user, AppRoles.Admin))
        {
            return;
        }

        var admins = await userManager.GetUsersInRoleAsync(AppRoles.Admin);
        if (admins.Count(item => item.IsActive && item.Id != user.Id) == 0)
        {
            throw new BusinessRuleException("A última conta administrativa ativa não pode perder esse acesso.");
        }
    }

    private static void EnsureIdentitySucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new BusinessRuleException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }

    private static string GenerateTemporaryPassword()
    {
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@$%*-_";
        const string all = lower + upper + digits + symbols;
        Span<char> password = stackalloc char[18];
        password[0] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[1] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        for (var index = 4; index < password.Length; index++)
        {
            password[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(password);
        return new string(password);
    }
}
