using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Content;
using PainelEstetica.Application.Security;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.Infrastructure.Services;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Security;

namespace PainelEstetica.WebAPI.Modules.Content;

public static class ContentEndpoints
{
    public static void MapPublicContentEndpoints(this RouteGroupBuilder publicApi)
    {
        publicApi.MapGet("/cms", async (IContentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken)));

        publicApi.MapGet("/cms/photo", async (
            EsteticaDbContext db,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var photoUrl = await db.CmsContents.AsNoTracking()
                .Select(content => content.DoctorPhotoUrl)
                .FirstOrDefaultAsync(cancellationToken);
            if (!TryGetStoredPhotoName(photoUrl, out var storedName)) return Results.NotFound();

            var path = FileUploadSecurity.ResolveStoredFile(configuration, "cms-images", storedName);
            return !File.Exists(path) ? Results.NotFound() : Results.File(path, "image/jpeg", enableRangeProcessing: true);
        });

        publicApi.MapGet("/cms/logo/{variant}", async (
            string variant,
            EsteticaDbContext db,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!IsLogoVariant(variant)) return Results.NotFound();

            var logoUrl = variant.Equals("dark", StringComparison.OrdinalIgnoreCase)
                ? await db.CmsContents.AsNoTracking().Select(content => content.LogoOnDarkUrl).FirstOrDefaultAsync(cancellationToken)
                : await db.CmsContents.AsNoTracking().Select(content => content.LogoOnLightUrl).FirstOrDefaultAsync(cancellationToken);
            if (!TryGetStoredLogoName(logoUrl, variant, out var storedName)) return Results.NotFound();

            var path = FileUploadSecurity.ResolveStoredFile(configuration, "cms-logos", storedName);
            return !File.Exists(path) ? Results.NotFound() : Results.File(path, "image/png", enableRangeProcessing: true);
        });

        publicApi.MapGet("/cms/image/{storedName}", (
            string storedName,
            IConfiguration configuration) =>
        {
            if (!TryGetStoredImageName(storedName, out var safeName)) return Results.NotFound();

            var path = FileUploadSecurity.ResolveStoredFile(configuration, "cms-images", safeName);
            return !File.Exists(path) ? Results.NotFound() : Results.File(path, "image/jpeg", enableRangeProcessing: true);
        });
    }

    public static void MapContentEndpoints(this RouteGroupBuilder securedApi)
    {
        securedApi.MapPut("/cms", async (
            UpdateCmsRequest request,
            HttpContext context,
            IContentService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var content = await service.UpdateAsync(request, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Cms.Updated", "CmsContent", content.Id.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(content);
        })
            .WithTags("CMS")
            .AddEndpointFilter<ValidationFilter<UpdateCmsRequest>>()
            .RequireAuthorization(AppPermissions.CmsManage)
            .Mutating();

        securedApi.MapPost("/cms/photo", async (
            IFormFile file,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var content = await db.CmsContents.FirstOrDefaultAsync(cancellationToken);
            if (content is null) return Results.NotFound();

            var originalFileName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}.jpg";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, "cms-images", storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            var previousPhotoUrl = content.DoctorPhotoUrl;

            try
            {
                var processed = await ClinicalPhotoProcessor.ProcessAsync(file, temporary, cancellationToken);
                File.Move(temporary, target);
                content.DoctorPhotoUrl = $"/api/public/cms/photo?v={storedName}";

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }

                if (TryGetStoredPhotoName(previousPhotoUrl, out var previousStoredName))
                {
                    SecureFileWriter.TryDelete(FileUploadSecurity.ResolveStoredFile(configuration, "cms-images", previousStoredName));
                }

                await audit.WriteAsync(
                    context.ToAuditContext(),
                    "Cms.PhotoUploaded",
                    "CmsContent",
                    content.Id.ToString(),
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

                return Results.Ok(content.ToDto());
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .WithTags("CMS")
            .RequireAuthorization(AppPermissions.CmsManage)
            .RequireRateLimiting("uploads")
            .Mutating();

        securedApi.MapPost("/cms/logo/{variant}", async (
            string variant,
            IFormFile file,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            if (!IsLogoVariant(variant))
            {
                throw new ArgumentException("Escolha uma versão de logo válida.");
            }

            var content = await db.CmsContents.FirstOrDefaultAsync(cancellationToken);
            if (content is null) return Results.NotFound();

            var originalFileName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}.png";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, "cms-logos", storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            var previousLogoUrl = GetLogoUrl(content, variant);

            try
            {
                var processed = await BrandLogoProcessor.ProcessAsync(file, temporary, cancellationToken);
                File.Move(temporary, target);
                SetLogoUrl(content, variant, $"/api/public/cms/logo/{variant.ToLowerInvariant()}?v={storedName}");

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }

                if (TryGetStoredLogoName(previousLogoUrl, variant, out var previousStoredName))
                {
                    SecureFileWriter.TryDelete(FileUploadSecurity.ResolveStoredFile(configuration, "cms-logos", previousStoredName));
                }

                await audit.WriteAsync(
                    context.ToAuditContext(),
                    "Cms.LogoUploaded",
                    "CmsContent",
                    content.Id.ToString(),
                    details: new
                    {
                        Variant = variant,
                        OriginalFileName = originalFileName,
                        processed.OriginalFileSizeBytes,
                        processed.FileSizeBytes,
                        processed.Width,
                        processed.Height,
                        processed.Sha256
                    },
                    cancellationToken: cancellationToken);

                return Results.Ok(content.ToDto());
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .WithTags("CMS")
            .RequireAuthorization(AppPermissions.CmsManage)
            .RequireRateLimiting("uploads")
            .Mutating();

        securedApi.MapPost("/cms/images", async (
            IFormFile file,
            HttpContext context,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var originalFileName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}.jpg";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, "cms-images", storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");

            try
            {
                var processed = await ClinicalPhotoProcessor.ProcessAsync(file, temporary, cancellationToken);
                File.Move(temporary, target);

                await audit.WriteAsync(
                    context.ToAuditContext(),
                    "Cms.ImageUploaded",
                    "CmsContent",
                    "public-media",
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

                return Results.Ok(new CmsImageUploadDto($"/api/public/cms/image/{storedName}"));
            }
            catch
            {
                SecureFileWriter.TryDelete(target);
                throw;
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .WithTags("CMS")
            .RequireAuthorization(AppPermissions.CmsManage)
            .RequireRateLimiting("uploads")
            .Mutating();
    }

    private static bool TryGetStoredPhotoName(string? photoUrl, out string storedName)
    {
        storedName = string.Empty;
        const string prefix = "/api/public/cms/photo?v=";
        if (string.IsNullOrWhiteSpace(photoUrl) || !photoUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var candidate = photoUrl[prefix.Length..].Split('&', 2)[0];
        if (!candidate.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate, Path.GetFileName(candidate), StringComparison.Ordinal)) return false;

        storedName = candidate;
        return true;
    }

    private static bool TryGetStoredImageName(string? candidate, out string storedName)
    {
        storedName = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate) ||
            !candidate.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate, Path.GetFileName(candidate), StringComparison.Ordinal)) return false;

        storedName = candidate;
        return true;
    }

    private static bool IsLogoVariant(string variant) =>
        variant.Equals("dark", StringComparison.OrdinalIgnoreCase) ||
        variant.Equals("light", StringComparison.OrdinalIgnoreCase);

    private static string GetLogoUrl(CmsContent content, string variant) =>
        variant.Equals("dark", StringComparison.OrdinalIgnoreCase)
            ? content.LogoOnDarkUrl
            : content.LogoOnLightUrl;

    private static void SetLogoUrl(CmsContent content, string variant, string value)
    {
        if (variant.Equals("dark", StringComparison.OrdinalIgnoreCase))
        {
            content.LogoOnDarkUrl = value;
            return;
        }

        content.LogoOnLightUrl = value;
    }

    private static bool TryGetStoredLogoName(string? logoUrl, string variant, out string storedName)
    {
        storedName = string.Empty;
        var normalizedVariant = variant.ToLowerInvariant();
        var prefix = $"/api/public/cms/logo/{normalizedVariant}?v=";
        if (string.IsNullOrWhiteSpace(logoUrl) || !logoUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var candidate = logoUrl[prefix.Length..].Split('&', 2)[0];
        if (!candidate.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(candidate, Path.GetFileName(candidate), StringComparison.Ordinal)) return false;

        storedName = candidate;
        return true;
    }
}
