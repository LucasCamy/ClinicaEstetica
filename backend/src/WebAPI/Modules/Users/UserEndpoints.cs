using System.Security.Claims;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Identity;
using PainelEstetica.Application.Security;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder securedApi)
    {
        var users = securedApi.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization(AppPermissions.UserManage);

        users.MapGet("/", async (
            int? page,
            int? pageSize,
            string? search,
            IUserAccessService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(new PageRequest(page ?? 1, pageSize ?? 25, search), cancellationToken)));

        users.MapGet("/access-catalog", () => Results.Ok(new
        {
            roles = AppRoles.All,
            permissions = AppPermissions.All
        }));

        users.MapPost("/", async (
            CreateUserRequest request,
            HttpContext context,
            IUserAccessService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var actorId = GetActorId(context.User);
            var user = await service.CreateAsync(request, actorId, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "User.Created",
                "User",
                user.Id.ToString(),
                details: new { user.Roles },
                cancellationToken: cancellationToken);
            return Results.Created($"/api/users/{user.Id}", user);
        })
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
            .Mutating();

        users.MapPut("/{id:guid}/access", async (
            Guid id,
            UpdateUserAccessRequest request,
            HttpContext context,
            IUserAccessService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var user = await service.UpdateAccessAsync(id, request, GetActorId(context.User), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "User.AccessChanged",
                "User",
                id.ToString(),
                details: new { user.Roles, user.PermissionOverrides },
                cancellationToken: cancellationToken);
            return Results.Ok(user);
        })
            .AddEndpointFilter<ValidationFilter<UpdateUserAccessRequest>>()
            .Mutating();

        users.MapPut("/{id:guid}/status", async (
            Guid id,
            UpdateUserStatusRequest request,
            HttpContext context,
            IUserAccessService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var user = await service.UpdateStatusAsync(id, request.IsActive, GetActorId(context.User), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "User.StatusChanged",
                "User",
                id.ToString(),
                details: new { user.IsActive },
                cancellationToken: cancellationToken);
            return Results.Ok(user);
        })
            .AddEndpointFilter<ValidationFilter<UpdateUserStatusRequest>>()
            .Mutating();

        users.MapPost("/{id:guid}/reset-password", async (
            Guid id,
            HttpContext context,
            IUserAccessService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ResetPasswordAsync(id, GetActorId(context.User), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "User.PasswordResetByAdmin",
                "User",
                id.ToString(),
                cancellationToken: cancellationToken);
            return Results.Ok(result);
        }).Mutating();
    }

    private static Guid GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("Identidade da sessão inválida.");
    }
}
