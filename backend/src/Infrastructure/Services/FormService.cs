using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Forms;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class FormService(EsteticaDbContext db, IClock clock) : IFormService
{
    public async Task<IReadOnlyList<FormSummaryDto>> ListAsync(
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = db.FormTemplates.AsNoTracking().Include(template => template.Versions).AsQueryable();
        if (!includeArchived) query = query.Where(template => template.Status != FormTemplateStatus.Archived);
        var templates = await query
            .OrderByDescending(template => template.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
        return templates.Select(ToSummary).ToArray();
    }

    public async Task<FormDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var template = await db.FormTemplates
            .AsNoTracking()
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return template is null ? null : ToDetail(template);
    }

    public async Task<FormVersionDto?> GetVersionAsync(
        Guid id,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        var version = await db.FormVersions.AsNoTracking().SingleOrDefaultAsync(
            item => item.FormTemplateId == id && item.VersionNumber == versionNumber,
            cancellationToken);
        return version is null ? null : ToVersion(version);
    }

    public async Task<FormDetailDto> CreateAsync(
        CreateFormTemplateRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var emptySchema = FormSchemaCodec.Normalize(new FormSchemaDefinition(), requireFillableField: false);
        var template = new FormTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Description = request.Description.Trim(),
            Status = FormTemplateStatus.Draft,
            DraftSchemaJson = FormSchemaCodec.Serialize(emptySchema),
            DraftRevision = 1,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.FormTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(template);
    }

    public async Task<FormDetailDto> UpdateDraftAsync(
        Guid id,
        UpdateFormDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var template = await FindTrackedAsync(id, cancellationToken);
        EnsureEditable(template);
        EnsureRevision(template, request.DraftRevision);
        var schema = FormSchemaCodec.Normalize(request.Schema, requireFillableField: false);

        template.Name = request.Name.Trim();
        template.Category = request.Category.Trim();
        template.Description = request.Description.Trim();
        template.DraftSchemaJson = FormSchemaCodec.Serialize(schema);
        template.DraftRevision++;
        template.UpdatedByUserId = actorUserId;
        template.UpdatedAtUtc = clock.UtcNow;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDetail(template);
    }

    public async Task<FormVersionDto> PublishAsync(
        Guid id,
        PublishFormRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var template = await FindTrackedAsync(id, cancellationToken);
        EnsureEditable(template);
        EnsureRevision(template, request.DraftRevision);
        var schema = FormSchemaCodec.Normalize(
            FormSchemaCodec.Deserialize(template.DraftSchemaJson),
            requireFillableField: true);
        var schemaHash = FormSchemaCodec.Hash(schema);
        var latest = LatestVersion(template);
        if (latest is not null &&
            latest.SchemaHash == schemaHash &&
            latest.Name == template.Name &&
            latest.Category == template.Category &&
            latest.Description == template.Description)
        {
            throw new BusinessRuleException("O rascunho não possui alterações em relação à última versão publicada.");
        }

        var now = clock.UtcNow;
        var version = new FormVersion
        {
            Id = Guid.NewGuid(),
            FormTemplateId = template.Id,
            VersionNumber = (latest?.VersionNumber ?? 0) + 1,
            Name = template.Name,
            Category = template.Category,
            Description = template.Description,
            SchemaJson = FormSchemaCodec.Serialize(schema),
            SchemaHash = schemaHash,
            ChangeSummary = request.ChangeSummary.Trim(),
            PublishedByUserId = actorUserId,
            PublishedAtUtc = now
        };
        db.FormVersions.Add(version);
        template.Status = FormTemplateStatus.Published;
        template.DraftRevision++;
        template.UpdatedByUserId = actorUserId;
        template.UpdatedAtUtc = now;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToVersion(version);
    }

    public async Task<FormDetailDto> DuplicateAsync(
        Guid id,
        DuplicateFormRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var source = await db.FormTemplates.AsNoTracking().SingleOrDefaultAsync(
            template => template.Id == id,
            cancellationToken) ?? throw new ResourceNotFoundException("Formulário", id);
        var now = clock.UtcNow;
        var duplicate = new FormTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Category = source.Category,
            Description = source.Description,
            Status = FormTemplateStatus.Draft,
            DraftSchemaJson = source.DraftSchemaJson,
            DraftRevision = 1,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.FormTemplates.Add(duplicate);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(duplicate);
    }

    public async Task<FormDetailDto> ArchiveAsync(
        Guid id,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var template = await FindTrackedAsync(id, cancellationToken);
        if (template.Status == FormTemplateStatus.Archived) return ToDetail(template);
        template.Status = FormTemplateStatus.Archived;
        template.ArchivedAtUtc = clock.UtcNow;
        template.UpdatedAtUtc = clock.UtcNow;
        template.UpdatedByUserId = actorUserId;
        template.DraftRevision++;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDetail(template);
    }

    private async Task<FormTemplate> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.FormTemplates
            .Include(template => template.Versions)
            .SingleOrDefaultAsync(template => template.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Formulário", id);

    private async Task SaveWithConcurrencyMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "O rascunho mudou enquanto a operação era salva. Recarregue o formulário antes de continuar.");
        }
    }

    private static void EnsureEditable(FormTemplate template)
    {
        if (template.Status == FormTemplateStatus.Archived)
        {
            throw new BusinessRuleException("Formulários arquivados não podem ser alterados ou publicados.");
        }
    }

    private static void EnsureRevision(FormTemplate template, int receivedRevision)
    {
        if (template.DraftRevision != receivedRevision)
        {
            throw new ConflictException(
                "Este rascunho foi alterado em outra sessão. Recarregue o formulário antes de continuar.");
        }
    }

    private static FormVersion? LatestVersion(FormTemplate template) =>
        template.Versions.OrderByDescending(version => version.VersionNumber).FirstOrDefault();

    private static bool HasUnpublishedChanges(FormTemplate template)
    {
        var latest = LatestVersion(template);
        if (latest is null) return true;
        var schema = FormSchemaCodec.Deserialize(template.DraftSchemaJson);
        return latest.SchemaHash != FormSchemaCodec.Hash(schema) ||
               latest.Name != template.Name ||
               latest.Category != template.Category ||
               latest.Description != template.Description;
    }

    private static FormSummaryDto ToSummary(FormTemplate template) => new(
        template.Id,
        template.Name,
        template.Category,
        template.Description,
        template.Status,
        template.DraftRevision,
        FormSchemaCodec.Deserialize(template.DraftSchemaJson).Fields.Count,
        LatestVersion(template)?.VersionNumber,
        HasUnpublishedChanges(template),
        template.UpdatedAtUtc);

    private static FormDetailDto ToDetail(FormTemplate template) => new(
        template.Id,
        template.Name,
        template.Category,
        template.Description,
        template.Status,
        template.DraftRevision,
        FormSchemaCodec.Deserialize(template.DraftSchemaJson),
        HasUnpublishedChanges(template),
        template.CreatedAtUtc,
        template.UpdatedAtUtc,
        template.Versions
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new FormVersionSummaryDto(
                version.Id,
                version.VersionNumber,
                FormSchemaCodec.Deserialize(version.SchemaJson).Fields.Count,
                version.ChangeSummary,
                version.PublishedAtUtc,
                version.PublishedByUserId))
            .ToArray());

    private static FormVersionDto ToVersion(FormVersion version) => new(
        version.Id,
        version.FormTemplateId,
        version.VersionNumber,
        version.Name,
        version.Category,
        version.Description,
        FormSchemaCodec.Deserialize(version.SchemaJson),
        version.SchemaHash,
        version.ChangeSummary,
        version.PublishedAtUtc,
        version.PublishedByUserId);
}
