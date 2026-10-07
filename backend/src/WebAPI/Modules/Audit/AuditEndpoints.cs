using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Security;

namespace PainelEstetica.WebAPI.Modules.Audit;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this RouteGroupBuilder securedApi) =>
        securedApi.MapGet("/audit", async (
            int? page,
            int? pageSize,
            string? search,
            string? action,
            Guid? actorUserId,
            IAuditService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                new PageRequest(page ?? 1, pageSize ?? 25, search),
                action,
                actorUserId,
                cancellationToken)))
            .WithTags("Audit")
            .RequireAuthorization(AppPermissions.AuditRead);
}
