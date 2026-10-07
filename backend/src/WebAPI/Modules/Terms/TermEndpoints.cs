using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Security;
using PainelEstetica.Application.Terms;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Security;

namespace PainelEstetica.WebAPI.Modules.Terms;

public static class TermEndpoints
{
    private const string TemplateStorageCategory = "term-templates";
    private const string SubmissionStorageCategory = "terms";

    public static void MapTermEndpoints(this RouteGroupBuilder securedApi)
    {
        var terms = securedApi.MapGroup("/terms").WithTags("Terms");

        terms.MapGet("/", async (bool? includeArchived, ITermService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeArchived ?? false, cancellationToken)))
            .RequireAuthorization(AppPermissions.FormRead);

        terms.MapGet("/{id:guid}", async (Guid id, ITermService service, CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } term ? Results.Ok(term) : Results.NotFound())
            .RequireAuthorization(AppPermissions.FormRead);

        terms.MapGet("/{id:guid}/versions/{versionNumber:int}", async (Guid id, int versionNumber, ITermService service, CancellationToken cancellationToken) =>
            await service.GetVersionAsync(id, versionNumber, cancellationToken) is { } version ? Results.Ok(version) : Results.NotFound())
            .RequireAuthorization(AppPermissions.FormRead);

        terms.MapGet("/{id:guid}/draft-pdf/content", async (
            Guid id,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var term = await db.TermTemplates.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (term is null || string.IsNullOrWhiteSpace(term.DraftPdfPath)) return Results.NotFound();
            var result = PrivatePdf(term.DraftPdfPath, configuration);
            if (result is null) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "TermTemplate.DraftPdfViewed", "TermTemplate", id.ToString(), cancellationToken: cancellationToken);
            return result;
        }).RequireAuthorization(AppPermissions.FormRead);

        terms.MapGet("/{id:guid}/versions/{versionNumber:int}/content", async (
            Guid id,
            int versionNumber,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var version = await db.TermVersions.AsNoTracking().SingleOrDefaultAsync(
                item => item.TermTemplateId == id && item.VersionNumber == versionNumber,
                cancellationToken);
            if (version is null) return Results.NotFound();
            var result = PrivatePdf(version.PdfPath, configuration);
            if (result is null) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "TermVersion.PdfViewed", "TermVersion", version.Id.ToString(), cancellationToken: cancellationToken);
            return result;
        }).RequireAuthorization(AppPermissions.FormRead);

        terms.MapPost("/", async (
            CreateTermTemplateRequest request,
            HttpContext context,
            ITermService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var term = await service.CreateAsync(request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermTemplate.Created", "TermTemplate", term.Id.ToString(), cancellationToken: cancellationToken);
            return Results.Created($"/api/terms/{term.Id}", term);
        })
            .AddEndpointFilter<ValidationFilter<CreateTermTemplateRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        terms.MapPost("/{id:guid}/draft-pdf", async (
            Guid id,
            IFormFile file,
            [FromForm] int draftRevision,
            HttpContext context,
            ITermService service,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            if (draftRevision < 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["draftRevision"] = ["Informe a revisão atual do rascunho."] });
            }
            var originalName = FileUploadSecurity.GetSafeOriginalFileName(file.FileName);
            var validated = await FileUploadSecurity.ValidatePdfAsync(file, cancellationToken);
            var storedName = $"{Guid.NewGuid():N}{validated.Extension}";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, TemplateStorageCategory, storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            try
            {
                var stored = await SecureFileWriter.WriteAsync(file, temporary, FileUploadSecurity.MaxDocumentBytes, cancellationToken);
                File.Move(temporary, target);
                try
                {
                    var term = await service.ReplaceDraftPdfAsync(
                        id,
                        draftRevision,
                        new TermPdfFileSnapshot(storedName, originalName, stored.FileSizeBytes, stored.Sha256),
                        RequiredActorId(context),
                        cancellationToken);
                    await audit.WriteAsync(
                        context.ToAuditContext(),
                        "TermTemplate.DraftPdfUploaded",
                        "TermTemplate",
                        id.ToString(),
                        details: new { stored.FileSizeBytes, stored.Sha256 },
                        cancellationToken: cancellationToken);
                    return Results.Ok(term);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .Mutating()
            .RequireRateLimiting("uploads")
            .RequireAuthorization(AppPermissions.FormManage);

        terms.MapPut("/{id:guid}/draft", async (
            Guid id,
            UpdateTermDraftRequest request,
            HttpContext context,
            ITermService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var term = await service.UpdateDraftAsync(id, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermTemplate.DraftUpdated", "TermTemplate", id.ToString(), details: new { term.DraftRevision, FieldCount = term.DraftLayout.Fields.Count }, cancellationToken: cancellationToken);
            return Results.Ok(term);
        })
            .AddEndpointFilter<ValidationFilter<UpdateTermDraftRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        terms.MapPost("/{id:guid}/publish", async (
            Guid id,
            PublishTermRequest request,
            HttpContext context,
            ITermService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var version = await service.PublishAsync(id, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermVersion.Published", "TermVersion", version.Id.ToString(), details: new { version.TermTemplateId, version.VersionNumber, version.PdfSha256, version.LayoutHash }, cancellationToken: cancellationToken);
            return Results.Created($"/api/terms/{id}/versions/{version.VersionNumber}", version);
        })
            .AddEndpointFilter<ValidationFilter<PublishTermRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        terms.MapPost("/{id:guid}/archive", async (
            Guid id,
            HttpContext context,
            ITermService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var term = await service.ArchiveAsync(id, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermTemplate.Archived", "TermTemplate", id.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(term);
        })
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        securedApi.MapGet("/terms/available", async (ITermService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAvailableAsync(cancellationToken)))
            .WithTags("Term submissions")
            .RequireAuthorization(AppPermissions.ClinicalRead);

        MapSubmissions(securedApi);
    }

    private static void MapSubmissions(RouteGroupBuilder securedApi)
    {
        var submissions = securedApi.MapGroup("/clients/{clientId:guid}/term-submissions").WithTags("Term submissions");

        submissions.MapGet("/", async (Guid clientId, HttpContext context, ITermService service, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var items = await service.ListForClientAsync(clientId, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.ListViewed", "Client", clientId.ToString(), details: new { Count = items.Count }, cancellationToken: cancellationToken);
            return Results.Ok(items);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        submissions.MapGet("/{submissionId:guid}", async (Guid clientId, Guid submissionId, HttpContext context, ITermService service, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var item = await service.GetSubmissionAsync(clientId, submissionId, cancellationToken);
            if (item is null) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.Viewed", "TermSubmission", submissionId.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(item);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        submissions.MapPost("/", async (Guid clientId, CreateTermSubmissionRequest request, HttpContext context, ITermService service, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var item = await service.CreateSubmissionAsync(clientId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.Created", "TermSubmission", item.Id.ToString(), details: new { item.ClientId, item.AppointmentId, item.TermVersionId, item.VersionNumber }, cancellationToken: cancellationToken);
            return Results.Created($"/api/clients/{clientId}/term-submissions/{item.Id}", item);
        })
            .AddEndpointFilter<ValidationFilter<CreateTermSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPut("/{submissionId:guid}/draft", async (Guid clientId, Guid submissionId, UpdateTermSubmissionRequest request, HttpContext context, ITermService service, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var item = await service.UpdateDraftSubmissionAsync(clientId, submissionId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.DraftUpdated", "TermSubmission", item.Id.ToString(), details: new { item.Revision, item.ValuesHash }, cancellationToken: cancellationToken);
            return Results.Ok(item);
        })
            .AddEndpointFilter<ValidationFilter<UpdateTermSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapDelete("/{submissionId:guid}", async (Guid clientId, Guid submissionId, int revision, HttpContext context, ITermService service, IAuditService audit, CancellationToken cancellationToken) =>
        {
            if (revision < 1) return Results.ValidationProblem(new Dictionary<string, string[]> { ["revision"] = ["Informe a revisão atual do rascunho."] });
            await service.DeleteDraftSubmissionAsync(clientId, submissionId, revision, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.DraftDeleted", "TermSubmission", submissionId.ToString(), details: new { ClientId = clientId, Revision = revision }, cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPost("/{submissionId:guid}/finalize", async (
            Guid clientId,
            Guid submissionId,
            IFormFile file,
            [FromForm] string payload,
            HttpContext context,
            ITermService service,
            EsteticaDbContext db,
            IConfiguration configuration,
            StorageQuotaManager quotaManager,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            FinalizeTermSubmissionRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<FinalizeTermSubmissionRequest>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = ["O preenchimento do termo é inválido."] });
            }
            if (request is null || request.Revision < 1) return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = ["Informe a revisão atual e os campos do termo."] });
            var validated = await FileUploadSecurity.ValidatePdfAsync(file, cancellationToken);
            var storedName = $"{Guid.NewGuid():N}{validated.Extension}";
            var target = FileUploadSecurity.ResolveStoredFile(configuration, SubmissionStorageCategory, storedName);
            var temporary = FileUploadSecurity.ResolveStoredFile(configuration, "temporary", $"{Guid.NewGuid():N}.upload");
            try
            {
                var stored = await SecureFileWriter.WriteAsync(file, temporary, FileUploadSecurity.MaxDocumentBytes, cancellationToken);
                await using var quotaLease = await quotaManager.AcquireAsync(cancellationToken);
                await quotaManager.EnsureCanStoreAsync(db, clientId, stored.FileSizeBytes, cancellationToken);
                File.Move(temporary, target);
                try
                {
                    var item = await service.FinalizeSubmissionAsync(
                        clientId,
                        submissionId,
                        request,
                        new TermPdfFileSnapshot(storedName, "termo-finalizado.pdf", stored.FileSizeBytes, stored.Sha256),
                        RequiredActorId(context),
                        cancellationToken);
                    await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.Finalized", "TermSubmission", item.Id.ToString(), details: new { item.TermVersionId, item.VersionNumber, item.ValuesHash, item.FinalPdfSha256, item.FinalPdfFileSizeBytes }, cancellationToken: cancellationToken);
                    return Results.Ok(item);
                }
                catch
                {
                    SecureFileWriter.TryDelete(target);
                    throw;
                }
            }
            finally
            {
                SecureFileWriter.TryDelete(temporary);
            }
        })
            .Mutating()
            .RequireRateLimiting("uploads")
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPost("/{submissionId:guid}/void", async (Guid clientId, Guid submissionId, VoidTermSubmissionRequest request, HttpContext context, ITermService service, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var item = await service.VoidSubmissionAsync(clientId, submissionId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.Voided", "TermSubmission", item.Id.ToString(), details: new { item.VoidedAtUtc, item.Revision }, cancellationToken: cancellationToken);
            return Results.Ok(item);
        })
            .AddEndpointFilter<ValidationFilter<VoidTermSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapGet("/{submissionId:guid}/content", async (Guid clientId, Guid submissionId, HttpContext context, EsteticaDbContext db, IConfiguration configuration, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var submission = await db.TermSubmissions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == submissionId && item.ClientId == clientId, cancellationToken);
            if (submission is null || string.IsNullOrWhiteSpace(submission.FinalPdfPath)) return Results.NotFound();
            var result = PrivatePdf(submission.FinalPdfPath, configuration);
            if (result is null) return Results.NotFound();
            await audit.WriteAsync(context.ToAuditContext(), "TermSubmission.PdfViewed", "TermSubmission", submission.Id.ToString(), cancellationToken: cancellationToken);
            return result;
        }).RequireAuthorization(AppPermissions.ClinicalRead);
    }

    private static IResult? PrivatePdf(string storedName, IConfiguration configuration)
    {
        var path = FileUploadSecurity.ResolveStoredFile(configuration, TemplateStorageCategory, storedName);
        if (!File.Exists(path))
        {
            path = FileUploadSecurity.ResolveStoredFile(configuration, SubmissionStorageCategory, storedName);
        }
        return File.Exists(path) ? Results.File(path, "application/pdf", enableRangeProcessing: true) : null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static Guid RequiredActorId(HttpContext context) =>
        context.ToAuditContext().ActorUserId
        ?? throw new UnauthorizedAccessException("A sessão não possui um usuário interno válido.");
}
