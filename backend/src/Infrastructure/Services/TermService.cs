using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Terms;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class TermService(EsteticaDbContext db, IClock clock) : ITermService
{
    private const int MaxValueLength = 2000;
    private const int MaxInkStrokes = 96;
    private const int MaxInkPoints = 12_000;
    private const int MaxPayloadBytes = 512 * 1024;

    public async Task<IReadOnlyList<TermTemplateSummaryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var query = db.TermTemplates.AsNoTracking().Include(template => template.Versions).AsQueryable();
        if (!includeArchived) query = query.Where(template => template.Status != TermTemplateStatus.Archived);
        var templates = await query.OrderByDescending(template => template.UpdatedAtUtc).ToListAsync(cancellationToken);
        return templates.Select(ToSummary).ToArray();
    }

    public async Task<TermTemplateDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var template = await db.TermTemplates.AsNoTracking().Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return template is null ? null : ToDetail(template);
    }

    public async Task<TermVersionDto?> GetVersionAsync(Guid id, int versionNumber, CancellationToken cancellationToken)
    {
        var version = await db.TermVersions.AsNoTracking().SingleOrDefaultAsync(
            item => item.TermTemplateId == id && item.VersionNumber == versionNumber,
            cancellationToken);
        return version is null ? null : ToVersion(version);
    }

    public async Task<TermTemplateDetailDto> CreateAsync(
        CreateTermTemplateRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var template = new TermTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Status = TermTemplateStatus.Draft,
            DraftLayoutJson = TermLayoutCodec.Serialize(TermLayoutCodec.Normalize(new TermLayoutDefinition())),
            DraftRevision = 1,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.TermTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToDetail(template);
    }

    public async Task<TermTemplateDetailDto> ReplaceDraftPdfAsync(
        Guid id,
        int draftRevision,
        TermPdfFileSnapshot pdf,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var template = await FindTemplateTrackedAsync(id, cancellationToken);
        EnsureEditable(template);
        EnsureRevision(template.DraftRevision, draftRevision, "O rascunho do termo mudou enquanto o arquivo era enviado. Recarregue e tente novamente.");
        EnsurePdfSnapshot(pdf);

        template.DraftPdfPath = pdf.StoredFileName;
        template.DraftPdfFileName = pdf.OriginalFileName;
        template.DraftPdfFileSizeBytes = pdf.FileSizeBytes;
        template.DraftPdfSha256 = pdf.Sha256;
        Touch(template, actorUserId);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDetail(template);
    }

    public async Task<TermTemplateDetailDto> UpdateDraftAsync(
        Guid id,
        UpdateTermDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var template = await FindTemplateTrackedAsync(id, cancellationToken);
        EnsureEditable(template);
        EnsureRevision(template.DraftRevision, request.DraftRevision, "O rascunho mudou enquanto era salvo. Recarregue o termo antes de continuar.");
        var layout = TermLayoutCodec.Normalize(request.Layout);
        template.Name = request.Name.Trim();
        template.Description = request.Description.Trim();
        template.DraftLayoutJson = TermLayoutCodec.Serialize(layout);
        Touch(template, actorUserId);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDetail(template);
    }

    public async Task<TermVersionDto> PublishAsync(
        Guid id,
        PublishTermRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var template = await FindTemplateTrackedAsync(id, cancellationToken);
        EnsureEditable(template);
        EnsureRevision(template.DraftRevision, request.DraftRevision, "O rascunho mudou enquanto era publicado. Recarregue o termo antes de continuar.");
        if (string.IsNullOrWhiteSpace(template.DraftPdfPath) || string.IsNullOrWhiteSpace(template.DraftPdfSha256))
        {
            throw new BusinessRuleException("Envie um PDF antes de publicar o termo.");
        }
        var layout = TermLayoutCodec.Normalize(TermLayoutCodec.Deserialize(template.DraftLayoutJson));
        if (layout.Fields.Count == 0)
        {
            throw new BusinessRuleException("Inclua pelo menos um campo preenchível antes de publicar o termo.");
        }
        var layoutHash = TermLayoutCodec.Hash(layout);
        var latest = LatestVersion(template);
        if (latest is not null &&
            latest.PdfSha256 == template.DraftPdfSha256 &&
            latest.LayoutHash == layoutHash &&
            latest.Name == template.Name &&
            latest.Description == template.Description)
        {
            throw new BusinessRuleException("O rascunho não possui alterações em relação à última versão publicada.");
        }

        var now = clock.UtcNow;
        var version = new TermVersion
        {
            Id = Guid.NewGuid(),
            TermTemplateId = template.Id,
            VersionNumber = (latest?.VersionNumber ?? 0) + 1,
            Name = template.Name,
            Description = template.Description,
            PdfPath = template.DraftPdfPath,
            PdfFileName = template.DraftPdfFileName ?? "termo.pdf",
            PdfFileSizeBytes = template.DraftPdfFileSizeBytes,
            PdfSha256 = template.DraftPdfSha256,
            LayoutJson = TermLayoutCodec.Serialize(layout),
            LayoutHash = layoutHash,
            ChangeSummary = request.ChangeSummary.Trim(),
            PublishedByUserId = actorUserId,
            PublishedAtUtc = now
        };
        db.TermVersions.Add(version);
        template.Status = TermTemplateStatus.Published;
        Touch(template, actorUserId);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToVersion(version);
    }

    public async Task<TermTemplateDetailDto> ArchiveAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken)
    {
        var template = await FindTemplateTrackedAsync(id, cancellationToken);
        if (template.Status == TermTemplateStatus.Archived) return ToDetail(template);
        template.Status = TermTemplateStatus.Archived;
        template.ArchivedAtUtc = clock.UtcNow;
        Touch(template, actorUserId);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDetail(template);
    }

    public async Task<IReadOnlyList<AvailableTermVersionDto>> ListAvailableAsync(CancellationToken cancellationToken)
    {
        var templates = await db.TermTemplates.AsNoTracking().Include(item => item.Versions)
            .Where(item => item.Status == TermTemplateStatus.Published)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return templates.Select(template => template.Versions.OrderByDescending(version => version.VersionNumber).FirstOrDefault())
            .Where(version => version is not null)
            .Select(version => new AvailableTermVersionDto(
                version!.TermTemplateId,
                version.Id,
                version.VersionNumber,
                version.Name,
                version.Description,
                version.PdfSha256,
                TermLayoutCodec.Deserialize(version.LayoutJson),
                version.LayoutHash))
            .ToArray();
    }

    public async Task<IReadOnlyList<TermSubmissionSummaryDto>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        await EnsureClientAsync(clientId, requireActive: false, cancellationToken);
        var submissions = await db.TermSubmissions.AsNoTracking().Include(item => item.TermVersion)
            .Where(item => item.ClientId == clientId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
        return submissions.Select(item =>
        {
            var layout = TermLayoutCodec.Deserialize(item.TermVersion.LayoutJson);
            var payload = ParsePayload(item.ValuesJson);
            var signatureFields = layout.Fields.Where(field => field.Type == TermFieldType.Signature).Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
            var isSigned = payload.Ink.Any(ink => signatureFields.Contains(ink.FieldId));
            return new TermSubmissionSummaryDto(
                item.Id, item.ClientId, item.AppointmentId, item.TermVersionId,
                item.TermVersion.Name, item.TermVersion.VersionNumber, item.Status, item.Revision, isSigned,
                item.CreatedAtUtc, item.UpdatedAtUtc, item.FinalizedAtUtc);
        }).ToArray();
    }

    public async Task<TermSubmissionDto?> GetSubmissionAsync(Guid clientId, Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await QuerySubmissions(tracking: false).SingleOrDefaultAsync(
            item => item.Id == submissionId && item.ClientId == clientId,
            cancellationToken);
        return submission is null ? null : ToSubmissionDto(submission);
    }

    public async Task<TermSubmissionDto> CreateSubmissionAsync(
        Guid clientId,
        CreateTermSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await EnsureClientAsync(clientId, requireActive: true, cancellationToken);
        var version = await db.TermVersions.Include(item => item.Template).SingleOrDefaultAsync(
            item => item.Id == request.TermVersionId,
            cancellationToken) ?? throw new ResourceNotFoundException("Versão do termo", request.TermVersionId);
        var latestVersionId = await db.TermVersions.Where(item => item.TermTemplateId == version.TermTemplateId)
            .OrderByDescending(item => item.VersionNumber).Select(item => item.Id).FirstAsync(cancellationToken);
        if (version.Template.Status != TermTemplateStatus.Published || latestVersionId != version.Id)
        {
            throw new BusinessRuleException("Inicie novos termos apenas pela versão publicada mais recente.");
        }
        await EnsureAppointmentAsync(clientId, request.AppointmentId, cancellationToken);
        var empty = NormalizePayload(TermLayoutCodec.Deserialize(version.LayoutJson),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal), [], false);
        var now = clock.UtcNow;
        var submission = new TermSubmission
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            AppointmentId = request.AppointmentId,
            TermVersionId = version.Id,
            TermVersion = version,
            Status = TermSubmissionStatus.Draft,
            ValuesJson = empty.Json,
            ValuesHash = empty.Hash,
            Revision = 1,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.TermSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);
        return ToSubmissionDto(submission);
    }

    public async Task<TermSubmissionDto> UpdateDraftSubmissionAsync(
        Guid clientId,
        Guid submissionId,
        UpdateTermSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var submission = await FindSubmissionTrackedAsync(clientId, submissionId, cancellationToken);
        EnsureSubmissionStatus(submission, TermSubmissionStatus.Draft, "Somente termos em rascunho podem ser alterados.");
        EnsureRevision(submission.Revision, request.Revision, "O termo foi modificado em outra sessão. Recarregue antes de salvar.");
        var payload = NormalizePayload(TermLayoutCodec.Deserialize(submission.TermVersion.LayoutJson), request.Values, request.Ink, false);
        ApplyPayload(submission, payload, actorUserId);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToSubmissionDto(submission);
    }

    public async Task DeleteDraftSubmissionAsync(Guid clientId, Guid submissionId, int revision, CancellationToken cancellationToken)
    {
        var submission = await FindSubmissionTrackedAsync(clientId, submissionId, cancellationToken);
        EnsureSubmissionStatus(submission, TermSubmissionStatus.Draft, "Somente termos em rascunho podem ser excluídos.");
        EnsureRevision(submission.Revision, revision, "O termo foi modificado em outra sessão. Recarregue antes de excluir.");
        db.TermSubmissions.Remove(submission);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
    }

    public async Task<TermSubmissionDto> FinalizeSubmissionAsync(
        Guid clientId,
        Guid submissionId,
        FinalizeTermSubmissionRequest request,
        TermPdfFileSnapshot pdf,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        EnsurePdfSnapshot(pdf);
        var submission = await FindSubmissionTrackedAsync(clientId, submissionId, cancellationToken);
        EnsureSubmissionStatus(submission, TermSubmissionStatus.Draft, "Somente termos em rascunho podem ser finalizados.");
        EnsureRevision(submission.Revision, request.Revision, "O termo foi modificado em outra sessão. Recarregue antes de finalizar.");
        var payload = NormalizePayload(TermLayoutCodec.Deserialize(submission.TermVersion.LayoutJson), request.Values, request.Ink, true);
        var now = clock.UtcNow;
        submission.ValuesJson = payload.Json;
        submission.ValuesHash = payload.Hash;
        submission.FinalPdfPath = pdf.StoredFileName;
        submission.FinalPdfFileSizeBytes = pdf.FileSizeBytes;
        submission.FinalPdfSha256 = pdf.Sha256;
        submission.Status = TermSubmissionStatus.Finalized;
        submission.Revision++;
        submission.UpdatedByUserId = actorUserId;
        submission.UpdatedAtUtc = now;
        submission.FinalizedByUserId = actorUserId;
        submission.FinalizedAtUtc = now;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToSubmissionDto(submission);
    }

    public async Task<TermSubmissionDto> VoidSubmissionAsync(
        Guid clientId,
        Guid submissionId,
        VoidTermSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var submission = await FindSubmissionTrackedAsync(clientId, submissionId, cancellationToken);
        if (submission.Status == TermSubmissionStatus.Voided) return ToSubmissionDto(submission);
        EnsureSubmissionStatus(submission, TermSubmissionStatus.Finalized, "Somente termos finalizados podem ser anulados.");
        EnsureRevision(submission.Revision, request.Revision, "O termo foi modificado em outra sessão. Recarregue antes de anular.");
        submission.Status = TermSubmissionStatus.Voided;
        submission.VoidReason = request.Reason.Trim();
        submission.VoidedByUserId = actorUserId;
        submission.VoidedAtUtc = clock.UtcNow;
        submission.Revision++;
        submission.UpdatedByUserId = actorUserId;
        submission.UpdatedAtUtc = clock.UtcNow;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToSubmissionDto(submission);
    }

    private async Task<TermTemplate> FindTemplateTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.TermTemplates.Include(template => template.Versions).SingleOrDefaultAsync(template => template.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Termo", id);

    private IQueryable<TermSubmission> QuerySubmissions(bool tracking) =>
        (tracking ? db.TermSubmissions : db.TermSubmissions.AsNoTracking())
            .Include(item => item.TermVersion)
            .ThenInclude(item => item.Template);

    private async Task<TermSubmission> FindSubmissionTrackedAsync(Guid clientId, Guid submissionId, CancellationToken cancellationToken) =>
        await QuerySubmissions(tracking: true).SingleOrDefaultAsync(item => item.Id == submissionId && item.ClientId == clientId, cancellationToken)
        ?? throw new ResourceNotFoundException("Termo do cliente", submissionId);

    private async Task EnsureClientAsync(Guid clientId, bool requireActive, CancellationToken cancellationToken)
    {
        var query = db.Clients.Where(client => client.Id == clientId);
        if (requireActive) query = query.Where(client => client.IsActive && !client.IsDeleted);
        if (!await query.AnyAsync(cancellationToken)) throw new ResourceNotFoundException("Cliente", clientId);
    }

    private async Task EnsureAppointmentAsync(Guid clientId, Guid? appointmentId, CancellationToken cancellationToken)
    {
        if (!appointmentId.HasValue) return;
        if (!await db.Appointments.AnyAsync(item => item.Id == appointmentId && item.ClientId == clientId, cancellationToken))
        {
            throw new BusinessRuleException("O atendimento selecionado não pertence a este cliente.");
        }
    }

    private void Touch(TermTemplate template, Guid actorUserId)
    {
        template.DraftRevision++;
        template.UpdatedByUserId = actorUserId;
        template.UpdatedAtUtc = clock.UtcNow;
    }

    private static void EnsureEditable(TermTemplate template)
    {
        if (template.Status == TermTemplateStatus.Archived)
        {
            throw new BusinessRuleException("Termos arquivados não podem ser alterados ou publicados.");
        }
    }

    private static void EnsureSubmissionStatus(TermSubmission submission, TermSubmissionStatus expected, string message)
    {
        if (submission.Status != expected) throw new BusinessRuleException(message);
    }

    private static void EnsureRevision(int current, int received, string message)
    {
        if (current != received) throw new ConflictException(message);
    }

    private static void EnsurePdfSnapshot(TermPdfFileSnapshot pdf)
    {
        if (string.IsNullOrWhiteSpace(pdf.StoredFileName) || string.IsNullOrWhiteSpace(pdf.OriginalFileName) ||
            pdf.FileSizeBytes <= 0 || pdf.Sha256.Length != 64)
        {
            throw new ArgumentException("O arquivo PDF final é inválido.");
        }
    }

    private async Task SaveWithConcurrencyMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("O registro foi alterado em outra sessão. Recarregue antes de continuar.");
        }
    }

    private static TermVersion? LatestVersion(TermTemplate template) => template.Versions.OrderByDescending(version => version.VersionNumber).FirstOrDefault();

    private static bool HasUnpublishedChanges(TermTemplate template)
    {
        var latest = LatestVersion(template);
        if (latest is null) return true;
        if (string.IsNullOrWhiteSpace(template.DraftPdfSha256)) return true;
        var layout = TermLayoutCodec.Deserialize(template.DraftLayoutJson);
        return latest.PdfSha256 != template.DraftPdfSha256 || latest.LayoutHash != TermLayoutCodec.Hash(layout) ||
               latest.Name != template.Name || latest.Description != template.Description;
    }

    private static TermTemplateSummaryDto ToSummary(TermTemplate template) => new(
        template.Id, template.Name, template.Description, template.Status, template.DraftRevision,
        !string.IsNullOrWhiteSpace(template.DraftPdfPath), TermLayoutCodec.Deserialize(template.DraftLayoutJson).Fields.Count,
        LatestVersion(template)?.VersionNumber, HasUnpublishedChanges(template), template.UpdatedAtUtc);

    private static TermTemplateDetailDto ToDetail(TermTemplate template) => new(
        template.Id, template.Name, template.Description, template.Status, template.DraftRevision,
        !string.IsNullOrWhiteSpace(template.DraftPdfPath), template.DraftPdfFileName, template.DraftPdfFileSizeBytes,
        template.DraftPdfSha256, TermLayoutCodec.Deserialize(template.DraftLayoutJson), HasUnpublishedChanges(template),
        template.CreatedAtUtc, template.UpdatedAtUtc,
        template.Versions.OrderByDescending(version => version.VersionNumber).Select(version => new TermVersionSummaryDto(
            version.Id, version.VersionNumber, TermLayoutCodec.Deserialize(version.LayoutJson).Fields.Count, version.ChangeSummary,
            version.PublishedAtUtc, version.PublishedByUserId, version.PdfFileSizeBytes, version.PdfSha256)).ToArray());

    private static TermVersionDto ToVersion(TermVersion version) => new(
        version.Id, version.TermTemplateId, version.VersionNumber, version.Name, version.Description,
        version.PdfFileName, version.PdfFileSizeBytes, version.PdfSha256, TermLayoutCodec.Deserialize(version.LayoutJson),
        version.LayoutHash, version.ChangeSummary, version.PublishedAtUtc, version.PublishedByUserId);

    private void ApplyPayload(TermSubmission submission, NormalizedPayload payload, Guid actorUserId)
    {
        submission.ValuesJson = payload.Json;
        submission.ValuesHash = payload.Hash;
        submission.Revision++;
        submission.UpdatedByUserId = actorUserId;
        submission.UpdatedAtUtc = clock.UtcNow;
    }

    private static NormalizedPayload NormalizePayload(
        TermLayoutDefinition layout,
        IDictionary<string, JsonElement>? rawValues,
        IReadOnlyList<TermInkCaptureRequest>? rawInk,
        bool requireRequired)
    {
        rawValues ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        rawInk ??= [];
        var fields = layout.Fields.ToDictionary(field => field.Id, StringComparer.Ordinal);
        var values = new JsonObject();
        foreach (var pair in rawValues)
        {
            var fieldId = (pair.Key ?? string.Empty).Trim().ToLowerInvariant();
            if (!fields.TryGetValue(fieldId, out var field) || field.Type is TermFieldType.Handwriting or TermFieldType.Signature)
            {
                throw new ArgumentException("O termo contém um valor para um campo inexistente ou não editável.");
            }
            var value = NormalizeValue(field, pair.Value);
            if (value is not null) values[fieldId] = value;
        }

        var inkByField = new Dictionary<string, NormalizedInk>(StringComparer.Ordinal);
        foreach (var capture in rawInk)
        {
            if (capture is null) throw new ArgumentException("O termo contém uma anotação vazia.");
            var fieldId = (capture.FieldId ?? string.Empty).Trim().ToLowerInvariant();
            if (!fields.TryGetValue(fieldId, out var field) || field.Type is not (TermFieldType.Handwriting or TermFieldType.Signature) || inkByField.ContainsKey(fieldId))
            {
                throw new ArgumentException("Cada anotação deve corresponder a um único campo de escrita ou assinatura.");
            }
            inkByField.Add(fieldId, NormalizeInk(field, capture));
        }

        if (requireRequired)
        {
            foreach (var field in layout.Fields.Where(field => field.Required))
            {
                var fulfilled = field.Type is TermFieldType.Handwriting or TermFieldType.Signature
                    ? inkByField.ContainsKey(field.Id)
                    : values.TryGetPropertyValue(field.Id, out var value) && ValueIsPresent(field, value);
                if (!fulfilled) throw new BusinessRuleException($"Preencha o campo obrigatório '{field.Label}'.");
            }
        }

        var ink = new JsonArray();
        foreach (var item in inkByField.Values.OrderBy(item => item.FieldId, StringComparer.Ordinal))
        {
            var strokes = new JsonArray();
            foreach (var stroke in item.Strokes)
            {
                var points = new JsonArray();
                foreach (var point in stroke) points.Add(new JsonArray(point.X, point.Y));
                strokes.Add(points);
            }
            var capture = new JsonObject
            {
                ["fieldId"] = item.FieldId,
                ["pointerType"] = item.PointerType,
                ["strokes"] = strokes
            };
            if (!string.IsNullOrWhiteSpace(item.SignerName)) capture["signerName"] = item.SignerName;
            if (item.DeclarationAccepted) capture["declarationAccepted"] = true;
            ink.Add(capture);
        }

        var root = new JsonObject { ["values"] = values, ["ink"] = ink };
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes) throw new ArgumentException("O preenchimento do termo excede o tamanho permitido.");
        return new NormalizedPayload(json, Sha256(json));
    }

    private static JsonNode? NormalizeValue(TermFieldDefinition field, JsonElement value)
    {
        switch (field.Type)
        {
            case TermFieldType.Text:
                if (value.ValueKind != JsonValueKind.String) throw InvalidValue(field);
                var text = (value.GetString() ?? string.Empty).Trim();
                if (text.Length > MaxValueLength) throw new ArgumentException($"O campo '{field.Label}' excede {MaxValueLength} caracteres.");
                return string.IsNullOrWhiteSpace(text) ? null : JsonValue.Create(text);
            case TermFieldType.Date:
                if (value.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw InvalidValue(field);
                return JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            case TermFieldType.Checkbox:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidValue(field);
                return JsonValue.Create(value.GetBoolean());
            default:
                throw InvalidValue(field);
        }
    }

    private static NormalizedInk NormalizeInk(TermFieldDefinition field, TermInkCaptureRequest capture)
    {
        if (capture.Strokes is null || capture.Strokes.Count is < 1 or > MaxInkStrokes ||
            capture.Strokes.Any(stroke => stroke is null || stroke.Count < 2 || stroke.Any(point => point is null)) ||
            capture.Strokes.Sum(stroke => stroke.Count) > MaxInkPoints)
        {
            throw new ArgumentException($"A escrita em '{field.Label}' deve possuir traços válidos.");
        }
        var pointerType = capture.PointerType?.Trim().ToLowerInvariant() ?? "unknown";
        if (pointerType is not ("touch" or "pen" or "mouse" or "unknown")) pointerType = "unknown";
        var signerName = field.Type == TermFieldType.Signature ? (capture.SignerName ?? string.Empty).Trim() : null;
        if (field.Type == TermFieldType.Signature && signerName!.Length is < 2 or > 160)
        {
            throw new ArgumentException($"Informe o nome do signatário para '{field.Label}'.");
        }
        if (field.Type == TermFieldType.Signature && !capture.DeclarationAccepted)
        {
            throw new ArgumentException($"Confirme a declaração de aceite para '{field.Label}'.");
        }
        var strokes = capture.Strokes.Select(stroke => stroke.Select(point =>
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X is < 0 or > 1 || point.Y is < 0 or > 1)
            {
                throw new ArgumentException($"A escrita em '{field.Label}' possui coordenadas inválidas.");
            }
            return new TermInkPointRequest { X = Math.Round(point.X, 4), Y = Math.Round(point.Y, 4) };
        }).ToArray()).ToArray();
        return new NormalizedInk(field.Id, signerName, pointerType, field.Type == TermFieldType.Signature, strokes);
    }

    private static bool ValueIsPresent(TermFieldDefinition field, JsonNode? value) => field.Type switch
    {
        TermFieldType.Checkbox => value?.GetValue<bool>() == true,
        _ => value is JsonValue item && !string.IsNullOrWhiteSpace(item.GetValue<string>())
    };

    private static ArgumentException InvalidValue(TermFieldDefinition field) => new($"O valor de '{field.Label}' não corresponde ao tipo {field.Type}.");

    private static TermSubmissionPayloadDto ParsePayload(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (root.TryGetProperty("values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in valuesElement.EnumerateObject()) values[property.Name] = property.Value.Clone();
        }
        var ink = new List<TermInkCaptureDto>();
        if (root.TryGetProperty("ink", out var inkElement) && inkElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in inkElement.EnumerateArray())
            {
                var strokes = item.GetProperty("strokes").EnumerateArray().Select(stroke =>
                    (IReadOnlyList<TermInkPointRequest>)stroke.EnumerateArray().Select(point => new TermInkPointRequest
                    {
                        X = point[0].GetDouble(), Y = point[1].GetDouble()
                    }).ToArray()).ToArray();
                ink.Add(new TermInkCaptureDto(
                    item.GetProperty("fieldId").GetString() ?? string.Empty,
                    item.TryGetProperty("signerName", out var signer) ? signer.GetString() : null,
                    item.GetProperty("pointerType").GetString() ?? "unknown",
                    item.TryGetProperty("declarationAccepted", out var declaration) && declaration.ValueKind == JsonValueKind.True,
                    strokes));
            }
        }
        return new TermSubmissionPayloadDto(values, ink);
    }

    private static TermSubmissionDto ToSubmissionDto(TermSubmission submission) => new(
        submission.Id, submission.ClientId, submission.AppointmentId, submission.TermVersionId,
        submission.TermVersion.TermTemplateId, submission.TermVersion.Name, submission.TermVersion.VersionNumber,
        submission.TermVersion.PdfSha256, TermLayoutCodec.Deserialize(submission.TermVersion.LayoutJson), submission.TermVersion.LayoutHash,
        submission.Status, submission.Revision, ParsePayload(submission.ValuesJson), submission.ValuesHash,
        submission.FinalPdfSha256, submission.FinalPdfFileSizeBytes, submission.CreatedByUserId, submission.UpdatedByUserId,
        submission.FinalizedByUserId, submission.CreatedAtUtc, submission.UpdatedAtUtc, submission.FinalizedAtUtc,
        submission.VoidedByUserId, submission.VoidedAtUtc, submission.VoidReason,
        submission.FinalPdfPath is null ? null : $"/api/clients/{submission.ClientId}/term-submissions/{submission.Id}/content");

    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record NormalizedPayload(string Json, string Hash);
    private sealed record NormalizedInk(string FieldId, string? SignerName, string PointerType, bool DeclarationAccepted, IReadOnlyList<IReadOnlyList<TermInkPointRequest>> Strokes);
}
