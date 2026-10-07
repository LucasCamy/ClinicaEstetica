using Microsoft.AspNetCore.Identity;

namespace PainelEstetica.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = [];
}

public sealed class UserPermissionOverride
{
    public Guid UserId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}
