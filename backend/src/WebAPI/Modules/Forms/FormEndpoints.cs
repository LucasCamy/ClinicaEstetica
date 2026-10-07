using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Forms;
using PainelEstetica.Application.Security;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Forms;

public static class FormEndpoints
{
    public static void MapFormEndpoints(this RouteGroupBuilder securedApi)
    {
        var forms = securedApi.MapGroup("/forms").WithTags("Forms");

        forms.MapGet("/", async (
            bool? includeArchived,
            IFormService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeArchived ?? false, cancellationToken)))
            .RequireAuthorization(AppPermissions.FormRead);

        forms.MapGet("/{id:guid}", async (
            Guid id,
            IFormService service,
            CancellationToken cancellationToken) =>
            await service.GetAsync(id, cancellationToken) is { } template
                ? Results.Ok(template)
                : Results.NotFound())
            .RequireAuthorization(AppPermissions.FormRead);

        forms.MapGet("/{id:guid}/versions/{versionNumber:int}", async (
            Guid id,
            int versionNumber,
            IFormService service,
            CancellationToken cancellationToken) =>
            await service.GetVersionAsync(id, versionNumber, cancellationToken) is { } version
                ? Results.Ok(version)
                : Results.NotFound())
            .RequireAuthorization(AppPermissions.FormRead);

        forms.MapPost("/", async (
            CreateFormTemplateRequest request,
            HttpContext context,
            IFormService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var actorId = RequiredActorId(context);
            var template = await service.CreateAsync(request, actorId, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormTemplate.Created",
                "FormTemplate",
                template.Id.ToString(),
                cancellationToken: cancellationToken);
            return Results.Created($"/api/forms/{template.Id}", template);
        })
            .AddEndpointFilter<ValidationFilter<CreateFormTemplateRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        forms.MapPut("/{id:guid}/draft", async (
            Guid id,
            UpdateFormDraftRequest request,
            HttpContext context,
            IFormService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var template = await service.UpdateDraftAsync(id, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormTemplate.DraftUpdated",
                "FormTemplate",
                id.ToString(),
                details: new { template.DraftRevision, FieldCount = template.DraftSchema.Fields.Count },
                cancellationToken: cancellationToken);
            return Results.Ok(template);
        })
            .AddEndpointFilter<ValidationFilter<UpdateFormDraftRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        forms.MapPost("/{id:guid}/publish", async (
            Guid id,
            PublishFormRequest request,
            HttpContext context,
            IFormService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var version = await service.PublishAsync(id, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormVersion.Published",
                "FormVersion",
                version.Id.ToString(),
                details: new { version.FormTemplateId, version.VersionNumber, version.SchemaHash },
                cancellationToken: cancellationToken);
            return Results.Created($"/api/forms/{id}/versions/{version.VersionNumber}", version);
        })
            .AddEndpointFilter<ValidationFilter<PublishFormRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        forms.MapPost("/{id:guid}/duplicate", async (
            Guid id,
            DuplicateFormRequest request,
            HttpContext context,
            IFormService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var duplicate = await service.DuplicateAsync(id, request, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormTemplate.Duplicated",
                "FormTemplate",
                duplicate.Id.ToString(),
                details: new { SourceFormTemplateId = id },
                cancellationToken: cancellationToken);
            return Results.Created($"/api/forms/{duplicate.Id}", duplicate);
        })
            .AddEndpointFilter<ValidationFilter<DuplicateFormRequest>>()
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);

        forms.MapPost("/{id:guid}/archive", async (
            Guid id,
            HttpContext context,
            IFormService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var template = await service.ArchiveAsync(id, RequiredActorId(context), cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "FormTemplate.Archived",
                "FormTemplate",
                id.ToString(),
                cancellationToken: cancellationToken);
            return Results.Ok(template);
        })
            .Mutating()
            .RequireAuthorization(AppPermissions.FormManage);
    }

    private static Guid RequiredActorId(HttpContext context) =>
        context.ToAuditContext().ActorUserId
        ?? throw new UnauthorizedAccessException("A sessão não possui um usuário interno válido.");
}
