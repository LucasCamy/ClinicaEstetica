using System.Text;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Forms;
using PainelEstetica.Application.Security;
using PainelEstetica.Infrastructure.Data;
using PainelEstetica.WebAPI.Endpoints;
using PainelEstetica.WebAPI.Security;

namespace PainelEstetica.WebAPI.Modules.Forms;

public static class FormSubmissionEndpoints
{
    public static void MapFormSubmissionEndpoints(this RouteGroupBuilder securedApi)
    {
        securedApi.MapGet("/forms/available", async (
            IFormSubmissionService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAvailableAsync(cancellationToken)))
            .WithTags("Form submissions")
            .RequireAuthorization(AppPermissions.ClinicalRead);

        var submissions = securedApi.MapGroup("/clients/{clientId:guid}/form-submissions")
            .WithTags("Form submissions");

        submissions.MapGet("/", async (
            Guid clientId,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var items = await service.ListForClientAsync(clientId, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.ListViewed",
                "Client",
                clientId.ToString(),
                details: new { Count = items.Count },
                cancellationToken: cancellationToken);
            return Results.Ok(items);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        submissions.MapGet("/{submissionId:guid}", async (
            Guid clientId,
            Guid submissionId,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var item = await service.GetAsync(clientId, submissionId, cancellationToken);
            if (item is null) return Results.NotFound();
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.Viewed",
                "FormSubmission",
                submissionId.ToString(),
                cancellationToken: cancellationToken);
            return Results.Ok(item);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        submissions.MapPost("/", async (
            Guid clientId,
            CreateFormSubmissionRequest request,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var item = await service.CreateAsync(clientId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.Created",
                "FormSubmission",
                item.Id.ToString(),
                details: new { item.ClientId, item.AppointmentId, item.FormVersionId, item.VersionNumber },
                cancellationToken: cancellationToken);
            return Results.Created($"/api/clients/{clientId}/form-submissions/{item.Id}", item);
        })
            .AddEndpointFilter<ValidationFilter<CreateFormSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPut("/{submissionId:guid}/draft", async (
            Guid clientId,
            Guid submissionId,
            UpdateFormSubmissionRequest request,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var item = await service.UpdateDraftAsync(clientId, submissionId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.DraftUpdated",
                "FormSubmission",
                item.Id.ToString(),
                details: new { item.Revision, item.FormVersionId },
                cancellationToken: cancellationToken);
            return Results.Ok(item);
        })
            .AddEndpointFilter<ValidationFilter<UpdateFormSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapDelete("/{submissionId:guid}", async (
            Guid clientId,
            Guid submissionId,
            int revision,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            if (revision < 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["revision"] = ["Informe a revisão atual do rascunho para excluí-lo."]
                });
            }

            await service.DeleteDraftAsync(clientId, submissionId, revision, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.DraftDeleted",
                "FormSubmission",
                submissionId.ToString(),
                details: new { ClientId = clientId, Revision = revision },
                cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPost("/{submissionId:guid}/finalize", async (
            Guid clientId,
            Guid submissionId,
            FinalizeFormSubmissionRequest request,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var item = await service.FinalizeAsync(clientId, submissionId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.Finalized",
                "FormSubmission",
                item.Id.ToString(),
                details: new
                {
                    item.FormVersionId,
                    item.VersionNumber,
                    item.SchemaHash,
                    item.OriginalAnswersHash,
                    item.FinalPdfSha256,
                    item.FinalPdfFileSizeBytes,
                    SignatureCount = item.Signatures.Count
                },
                cancellationToken: cancellationToken);
            return Results.Ok(item);
        })
            .AddEndpointFilter<ValidationFilter<FinalizeFormSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPost("/{submissionId:guid}/amend", async (
            Guid clientId,
            Guid submissionId,
            AmendFormSubmissionRequest request,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var item = await service.AmendAsync(clientId, submissionId, request, RequiredActorId(context), cancellationToken);
            var amendment = item.Amendments[^1];
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.Amended",
                "FormSubmission",
                item.Id.ToString(),
                details: new
                {
                    amendment.Id,
                    amendment.AmendmentNumber,
                    amendment.PreviousAnswersHash,
                    amendment.AnswersHash,
                    SignatureCount = item.Signatures.Count(signature => signature.AmendmentId == amendment.Id)
                },
                cancellationToken: cancellationToken);
            return Results.Ok(item);
        })
            .AddEndpointFilter<ValidationFilter<AmendFormSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapPost("/{submissionId:guid}/void", async (
            Guid clientId,
            Guid submissionId,
            VoidFormSubmissionRequest request,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var item = await service.VoidAsync(clientId, submissionId, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.Voided",
                "FormSubmission",
                item.Id.ToString(),
                details: new { item.VoidedAtUtc, item.Revision },
                cancellationToken: cancellationToken);
            return Results.Ok(item);
        })
            .AddEndpointFilter<ValidationFilter<VoidFormSubmissionRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.ClinicalManage);

        submissions.MapGet("/{submissionId:guid}/content", async (
            Guid clientId,
            Guid submissionId,
            HttpContext context,
            EsteticaDbContext db,
            IConfiguration configuration,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var storedName = await db.FormSubmissions.AsNoTracking()
                .Where(item => item.Id == submissionId && item.ClientId == clientId && item.FinalPdfPath != null)
                .Select(item => item.FinalPdfPath)
                .SingleOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(storedName)) return Results.NotFound();

            var path = FileUploadSecurity.ResolveStoredFile(configuration, "forms", storedName);
            if (!File.Exists(path)) return Results.NotFound();
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSubmission.PdfViewed",
                "FormSubmission",
                submissionId.ToString(),
                cancellationToken: cancellationToken);
            return Results.File(path, "application/pdf", enableRangeProcessing: true);
        }).RequireAuthorization(AppPermissions.ClinicalRead);

        submissions.MapGet("/{submissionId:guid}/signatures/{signatureId:guid}/content", async (
            Guid clientId,
            Guid submissionId,
            Guid signatureId,
            HttpContext context,
            IFormSubmissionService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var svg = await service.GetSignatureSvgAsync(clientId, submissionId, signatureId, cancellationToken);
            if (svg is null) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormSignature.Viewed",
                "FormSignature",
                signatureId.ToString(),
                cancellationToken: cancellationToken);
            return Results.Content(svg, "image/svg+xml", Encoding.UTF8);
        }).RequireAuthorization(AppPermissions.ClinicalRead);
    }

    private static Guid RequiredActorId(HttpContext context) =>
        context.ToAuditContext().ActorUserId
        ?? throw new UnauthorizedAccessException("A sessão não possui um usuário interno válido.");
}
