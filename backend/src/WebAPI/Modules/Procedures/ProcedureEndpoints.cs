using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Catalog;
using PainelEstetica.Application.Security;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Services;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Security;

namespace PainelEstetica.WebAPI.Modules.Procedures;

public static class ProcedureEndpoints
{
    public static void MapPublicProcedureEndpoints(this RouteGroupBuilder publicApi)
    {
        publicApi.MapGet("/procedures", async (IProcedureService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(publicOnly: true, cancellationToken)));

        publicApi.MapGet("/procedures/{id:guid}/image", async (
            Guid id,
            EsteticaDbContext db,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var procedure = await db.ProcedureTypes.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == id && item.IsActive && item.IsPublicWebsite,
                cancellationToken);
            if (procedure is null || !TryGetStoredImageName(procedure.ImageUrl, id, out var storedName)) return Results.NotFound();

            var path = FileUploadSecurity.ResolveStoredFile(configuration, "procedure-images", storedName);
            return !File.Exists(path) ? Results.NotFound() : Results.File(path, "image/jpeg", enableRangeProcessing: true);
        });
    }

    public static void MapProcedureEndpoints(this RouteGroupBuilder securedApi)
    {
        var procedures = securedApi.MapGroup("/procedures").WithTags("Procedures");
        procedures.MapGet("/", async (IProcedureService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(publicOnly: false, cancellationToken)))
            .RequireAuthorization(AppPermissions.ProcedureRead);

        procedures.MapPost("/", async (
            SaveProcedureRequest request,
            HttpContext context,
            IProcedureService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var procedure = await service.CreateAsync(request, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Procedure.Created", "Procedure", procedure.Id.ToString(), cancellationToken: cancellationToken);
            return Results.Created($"/api/procedures/{procedure.Id}", procedure);
        })
            .AddEndpointFilter<ValidationFilter<SaveProcedureRequest>>()
            .RequireAuthorization(AppPermissions.ProcedureManage)
            .Mutating();

        procedures.MapPut("/{id:guid}", async (
            Guid id,
            SaveProcedureRequest request,
            HttpContext context,
            IProcedureService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var procedure = await service.UpdateAsync(id, request, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Procedure.Updated", "Procedure", id.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(procedure);
        })
            .AddEndpointFilter<ValidationFilter<SaveProcedureRequest>>()
            .RequireAuthorization(AppPermissions.ProcedureManage)
            .Mutating();

        procedures.MapPost("/{id:guid}/image", async (
            Guid id,
            IFormFile file,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var procedure = await db.ProcedureTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (procedure is null) return Results.NotFound();

            var originalFileName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}.jpg";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, "procedure-images", storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            var previousImageUrl = procedure.ImageUrl;

            try
            {
                var processed = await ClinicalPhotoProcessor.ProcessAsync(file, temporary, cancellationToken);
                File.Move(temporary, target);
                procedure.ImageUrl = $"/api/public/procedures/{procedure.Id}/image?v={storedName}";

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }

                if (TryGetStoredImageName(previousImageUrl, procedure.Id, out var previousStoredName))
                {
                    SecureFileWriter.TryDelete(FileUploadSecurity.ResolveStoredFile(configuration, "procedure-images", previousStoredName));
                }

                await audit.WriteAsync(
                    context.ToAuditContext(),
                    "Procedure.ImageUploaded",
                    "Procedure",
                    procedure.Id.ToString(),
                    details: new
                    {
                        OriginalFileName = originalFileName,
                        processed.OriginalFileSizeBytes,
                        processed.FileSizeBytes,
                        processed.Width,
                        processed.Height,
                        processed.Sha256
                    },
                    cancellationToken: cancellationToken);

                return Results.Ok(procedure.ToDto());
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .RequireAuthorization(AppPermissions.ProcedureManage)
            .RequireRateLimiting("uploads")
            .Mutating();

        procedures.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IProcedureService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Procedure.Deactivated", "Procedure", id.ToString(), cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .RequireAuthorization(AppPermissions.ProcedureManage)
            .Mutating();
    }

    private static bool TryGetStoredImageName(string imageUrl, Guid procedureId, out string storedName)
    {
        storedName = string.Empty;
        var prefix = $"/api/public/procedures/{procedureId}/image?v=";
        if (!imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var candidate = imageUrl[prefix.Length..].Split('&', 2)[0];
        if (!candidate.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate, Path.GetFileName(candidate), StringComparison.Ordinal)) return false;

        storedName = candidate;
        return true;
    }
}
