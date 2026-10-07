using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Security;
using PainelEstetica.Application.Settings;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Security;

namespace PainelEstetica.WebAPI.Modules.Settings;

public static class ClinicSettingsEndpoints
{
    public static void MapClinicSettingsEndpoints(this RouteGroupBuilder securedApi)
    {
        var settings = securedApi.MapGroup("/settings").WithTags("Settings");

        settings.MapGet("/operational", async (IClinicSettingsService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetOperationalSettingsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.SettingsRead);

        settings.MapPut("/operational", async (
            SaveOperatingHoursRequest request,
            HttpContext context,
            IClinicSettingsService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateOperatingHoursAsync(request.OperatingHours, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "ClinicSettings.OperatingHoursUpdated",
                "ClinicOperationalSettings",
                details: new { updated.OperatingHours, updated.TimeZoneId },
                cancellationToken: cancellationToken);
            return Results.Ok(updated);
        })
            .AddEndpointFilter<ValidationFilter<SaveOperatingHoursRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.SettingsManage);

        settings.MapGet("/storage", async (
            EsteticaDbContext db,
            StorageQuotaManager quotaManager,
            CancellationToken cancellationToken) =>
        {
            var usage = await quotaManager.GetInstallationUsageAsync(db, cancellationToken);
            return Results.Ok(new StorageUsageDto(usage.InstallationUsedBytes, usage.InstallationLimitBytes));
        }).RequireAuthorization(AppPermissions.SettingsRead);
    }
}
