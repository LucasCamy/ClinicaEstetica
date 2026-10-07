using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Clients;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Security;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Services;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Security;

namespace PainelEstetica.WebAPI.Modules.Clients;

public static class ClientEndpoints
{
    public static void MapClientEndpoints(this RouteGroupBuilder securedApi)
    {
        var clients = securedApi.MapGroup("/clients").WithTags("Clients");

        clients.MapGet("/", async (
            int? page,
            int? pageSize,
            string? search,
            IClientService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(new PageRequest(page ?? 1, pageSize ?? 25, search), cancellationToken)))
            .RequireAuthorization(AppPermissions.ClientRead);

        clients.MapGet("/{id:guid}", async (Guid id, IClientService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } client
                ? Results.Ok(client)
                : Results.NotFound())
            .RequireAuthorization(AppPermissions.ClientRead);

        clients.MapGet("/{id:guid}/clinical", async (
            Guid id,
            HttpContext context,
            IClientService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var client = await service.GetClinicalAsync(id, cancellationToken);
            if (client is null) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "ClinicalRecord.Viewed", "Client", id.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(client);
        })
            .RequireAuthorization(AppPermissions.ClinicalRead);

        clients.MapGet("/{id:guid}/summary", async (
            Guid id,
            HttpContext context,
            IClientService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var summary = await service.GetSummaryAsync(id, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "ClientSummary.Viewed",
                "Client",
                id.ToString(),
                cancellationToken: cancellationToken);
            return Results.Ok(summary);
        })
            .RequireAuthorization(AppPermissions.ClinicalRead)
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin, AppRoles.Professional));

        clients.MapPost("/", async (
            SaveClientRequest request,
            HttpContext context,
            IClientService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var client = await service.CreateAsync(request, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Client.Created", "Client", client.Id.ToString(), cancellationToken: cancellationToken);
            return Results.Created($"/api/clients/{client.Id}", client);
        })
            .AddEndpointFilter<ValidationFilter<SaveClientRequest>>()
            .RequireAuthorization(AppPermissions.ClientManage)
            .Mutating();

        clients.MapPut("/{id:guid}", async (
            Guid id,
            SaveClientRequest request,
            HttpContext context,
            IClientService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var client = await service.UpdateAsync(id, request, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Client.Updated", "Client", id.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(client);
        })
            .AddEndpointFilter<ValidationFilter<SaveClientRequest>>()
            .RequireAuthorization(AppPermissions.ClientManage)
            .Mutating();

        clients.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IClientService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Client.Deactivated", "Client", id.ToString(), cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .RequireAuthorization(AppPermissions.ClientManage)
            .Mutating();

        clients.MapPost("/{id:guid}/medical-records", async (
            Guid id,
            CreateMedicalRecordRequest request,
            HttpContext context,
            IClientService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var record = await service.CreateMedicalRecordAsync(id, request, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "ClinicalRecord.Created", "MedicalRecord", record.Id.ToString(), cancellationToken: cancellationToken);
            return Results.Created($"/api/clients/{id}/medical-records/{record.Id}", record);
        })
            .AddEndpointFilter<ValidationFilter<CreateMedicalRecordRequest>>()
            .RequireAuthorization(AppPermissions.ClinicalManage)
            .Mutating();

        MapClinicalFiles(clients);
    }

    public static void MapPublicPhotoEndpoints(this RouteGroupBuilder publicApi)
    {
        publicApi.MapGet("/photos", async (EsteticaDbContext db, CancellationToken cancellationToken) =>
        {
            var photos = await db.ClientPhotos
                .AsNoTracking()
                .Where(photo => photo.IsPublicForWebsite && photo.ConsentGiven)
                .OrderByDescending(photo => photo.CreatedAt)
                .ToListAsync(cancellationToken);
            return Results.Ok(photos.Select(photo => new
            {
                photo.Id,
                photo.Type,
                photo.ProcedureName,
                photo.CreatedAt,
                contentUrl = $"/api/public/photos/{photo.Id}/content"
            }));
        });

        publicApi.MapGet("/photos/{photoId:guid}/content", async (
            Guid photoId,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var photo = await db.ClientPhotos.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == photoId && item.IsPublicForWebsite && item.ConsentGiven,
                cancellationToken);
            if (photo is null) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "ClinicalPhoto.Viewed", "ClientPhoto", photo.Id.ToString(), cancellationToken: cancellationToken);
            return PhotoFile(photo, configuration);
        });
    }

    private static void MapClinicalFiles(RouteGroupBuilder clients)
    {
        clients.MapPost("/{id:guid}/photos", async (
            Guid id,
            IFormFile file,
            [FromForm] PhotoType type,
            [FromForm] string procedureName,
            [FromForm] bool isPublicForWebsite,
            [FromForm] bool consentGiven,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            StorageQuotaManager quotaManager,
            IAuditService audit,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Clients.AnyAsync(client => client.Id == id, cancellationToken)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(procedureName) || procedureName.Length > 160)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["procedureName"] = ["Informe um procedimento com até 160 caracteres."]
                });
            }

            if (isPublicForWebsite && !consentGiven)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["consentGiven"] = ["Uma foto só pode ser pública quando houver consentimento registrado."]
                });
            }

            var originalFileName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}.jpg";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, "photos", storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            try
            {
                var processed = await ClinicalPhotoProcessor.ProcessAsync(file, temporary, cancellationToken);
                await using var quotaLease = await quotaManager.AcquireAsync(cancellationToken);
                await quotaManager.EnsureCanStoreAsync(db, id, processed.FileSizeBytes, cancellationToken);
                File.Move(temporary, target);

                var photo = new ClientPhoto
                {
                    Id = Guid.NewGuid(),
                    ClientId = id,
                    OriginalFileName = originalFileName,
                    FilePath = storedName,
                    ContentType = processed.ContentType,
                    OriginalFileSizeBytes = processed.OriginalFileSizeBytes,
                    FileSizeBytes = processed.FileSizeBytes,
                    Sha256 = processed.Sha256,
                    Width = processed.Width,
                    Height = processed.Height,
                    Type = type,
                    ProcedureName = procedureName.Trim(),
                    IsPublicForWebsite = isPublicForWebsite,
                    ConsentGiven = consentGiven,
                    CreatedAt = clock.UtcNow
                };
                db.ClientPhotos.Add(photo);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }

                await audit.WriteAsync(
                    context.ToAuditContext(),
                    "ClinicalPhoto.Uploaded",
                    "ClientPhoto",
                    photo.Id.ToString(),
                    details: new
                    {
                        photo.IsPublicForWebsite,
                        photo.ConsentGiven,
                        photo.OriginalFileSizeBytes,
                        photo.FileSizeBytes,
                        photo.Width,
                        photo.Height,
                        photo.Sha256
                    },
                    cancellationToken: cancellationToken);
                return Results.Created($"/api/clients/{id}/photos/{photo.Id}", photo.ToDto());
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .Mutating()
            .RequireRateLimiting("uploads")
            .RequireAuthorization(AppPermissions.ClinicalManage);

        clients.MapGet("/{id:guid}/photos/{photoId:guid}/content", async (
            Guid id,
            Guid photoId,
            EsteticaDbContext db,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var photo = await db.ClientPhotos.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == photoId && item.ClientId == id,
                cancellationToken);
            return photo is null ? Results.NotFound() : PhotoFile(photo, configuration);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        clients.MapPost("/{id:guid}/documents", async (
            Guid id,
            IFormFile file,
            [FromForm] string documentType,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            StorageQuotaManager quotaManager,
            IAuditService audit,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Clients.AnyAsync(client => client.Id == id, cancellationToken)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(documentType) || documentType.Length > 120)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["documentType"] = ["Informe um tipo de documento com até 120 caracteres."]
                });
            }

            var originalFileName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var validated = await FileUploadSecurity.ValidateDocumentAsync(file, cancellationToken);
            var storedName = $"{Guid.NewGuid():N}{validated.Extension}";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, "documents", storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            try
            {
                var stored = await SecureFileWriter.WriteAsync(
                    file,
                    temporary,
                    FileUploadSecurity.MaxDocumentBytes,
                    cancellationToken);
                await using var quotaLease = await quotaManager.AcquireAsync(cancellationToken);
                await quotaManager.EnsureCanStoreAsync(db, id, stored.FileSizeBytes, cancellationToken);
                File.Move(temporary, target);

                var document = new ClientDocument
                {
                    Id = Guid.NewGuid(),
                    ClientId = id,
                    FileName = originalFileName,
                    FilePath = storedName,
                    ContentType = validated.ContentType,
                    FileSizeBytes = stored.FileSizeBytes,
                    Sha256 = stored.Sha256,
                    DocumentType = documentType.Trim(),
                    UploadedAt = clock.UtcNow
                };
                db.ClientDocuments.Add(document);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }

                await audit.WriteAsync(
                    context.ToAuditContext(),
                    "ClinicalDocument.Uploaded",
                    "ClientDocument",
                    document.Id.ToString(),
                    details: new { document.FileSizeBytes, document.ContentType, document.Sha256 },
                    cancellationToken: cancellationToken);
                return Results.Created($"/api/clients/{id}/documents/{document.Id}", document.ToDto());
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .Mutating()
            .RequireRateLimiting("uploads")
            .RequireAuthorization(AppPermissions.ClinicalManage);

        clients.MapGet("/{id:guid}/documents/{documentId:guid}/content", async (
            Guid id,
            Guid documentId,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var document = await db.ClientDocuments.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == documentId && item.ClientId == id,
                cancellationToken);
            if (document is null) return Results.NotFound();
            var path = FileUploadSecurity.ResolveStoredFile(configuration, "documents", document.FilePath);
            if (!File.Exists(path)) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "ClinicalDocument.Viewed", "ClientDocument", document.Id.ToString(), cancellationToken: cancellationToken);
            return Results.File(
                path,
                string.IsNullOrWhiteSpace(document.ContentType)
                    ? FileUploadSecurity.GetDocumentContentType(document.FilePath)
                    : document.ContentType,
                document.FileName,
                enableRangeProcessing: true);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        clients.MapGet("/{id:guid}/storage-usage", async (
            Guid id,
            EsteticaDbContext db,
            StorageQuotaManager quotaManager,
            CancellationToken cancellationToken) =>
        {
            if (!await db.Clients.AnyAsync(client => client.Id == id, cancellationToken)) return Results.NotFound();
            return Results.Ok(await quotaManager.GetUsageAsync(db, id, cancellationToken));
        }).RequireAuthorization(AppPermissions.ClinicalRead);
    }

    private static IResult PhotoFile(ClientPhoto photo, IConfiguration configuration)
    {
        var path = FileUploadSecurity.ResolveStoredFile(configuration, "photos", photo.FilePath);
        if (!File.Exists(path)) return Results.NotFound();
        return Results.File(
            path,
            string.IsNullOrWhiteSpace(photo.ContentType) ? GetContentType(photo.FilePath) : photo.ContentType,
            enableRangeProcessing: true);
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
