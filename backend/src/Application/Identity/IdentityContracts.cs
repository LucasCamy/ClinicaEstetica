using System.ComponentModel.DataAnnotations;
using PainelEstetica.Application.Common;

namespace PainelEstetica.Application.Identity;

public sealed class LoginRequest
{
    [Required, StringLength(80)]
    public string Username { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Password { get; init; } = string.Empty;
}

public sealed class MfaLoginRequest
{
    [Required, StringLength(20)]
    public string Code { get; init; } = string.Empty;

    public bool RememberMachine { get; init; }
}

public sealed class RecoveryCodeLoginRequest
{
    [Required, StringLength(100)]
    public string Code { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required, StringLength(200)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed class EnableMfaRequest
{
    [Required, StringLength(20)]
    public string Code { get; init; } = string.Empty;
}

public sealed record LoginResultDto(string Status, CurrentUserDto? User = null);

public sealed record CurrentUserDto(
    Guid Id,
    string Username,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword,
    bool MfaEnabled,
    bool RequiresMfaSetup);

public sealed record MfaSetupDto(string SharedKey, string AuthenticatorUri);

public sealed record MfaRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

public sealed record UserPermissionOverrideDto(string Permission, bool IsGranted);

public sealed record UserAdminDto(
    Guid Id,
    string Username,
    string Email,
    bool IsActive,
    bool MustChangePassword,
    bool MfaEnabled,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<UserPermissionOverrideDto> PermissionOverrides,
    IReadOnlyList<string> EffectivePermissions);

public sealed class CreateUserRequest
{
    [Required, StringLength(80, MinimumLength = 3)]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 12)]
    public string TemporaryPassword { get; init; } = string.Empty;

    [Required, MinLength(1)]
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed class UpdateUserAccessRequest
{
    [Required, MinLength(1)]
    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<UserPermissionOverrideDto> PermissionOverrides { get; init; } = [];
}

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; init; }
}

public sealed record ResetPasswordDto(string TemporaryPassword);

public interface IUserAccessService
{
    Task<PagedResult<UserAdminDto>> ListAsync(PageRequest page, CancellationToken cancellationToken);
    Task<UserAdminDto> CreateAsync(CreateUserRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<UserAdminDto> UpdateAccessAsync(Guid id, UpdateUserAccessRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<UserAdminDto> UpdateStatusAsync(Guid id, bool isActive, Guid actorUserId, CancellationToken cancellationToken);
    Task<ResetPasswordDto> ResetPasswordAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken);
}
