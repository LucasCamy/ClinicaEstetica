using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Forms;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class FormSubmissionService(
    EsteticaDbContext db,
    IClock clock,
    IFormSubmissionPdfArchiver pdfArchiver) : IFormSubmissionService
{
    public const string SignerDeclaration =
        "Declaro que li e concordo com as informações e autorizações apresentadas neste formulário.";

    private const int MaxAnswersBytes = 256 * 1024;
    private const int MaxSignatureStrokes = 64;
    private const int MaxSignaturePoints = 8192;

    public async Task<IReadOnlyList<AvailableFormVersionDto>> ListAvailableAsync(CancellationToken cancellationToken)
    {
        var templates = await db.FormTemplates
            .AsNoTracking()
            .Include(template => template.Versions)
            .Where(template => template.Status == FormTemplateStatus.Published)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(template => template.Versions
                .OrderByDescending(version => version.VersionNumber)
                .FirstOrDefault())
            .Where(version => version is not null)
            .Select(version => new AvailableFormVersionDto(
                version!.FormTemplateId,
                version.Id,
                version.VersionNumber,
                version.Name,
                version.Category,
                version.Description,
                FormSchemaCodec.Deserialize(version.SchemaJson),
                version.SchemaHash))
            .ToArray();
    }

    public async Task<IReadOnlyList<FormSubmissionSummaryDto>> ListForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(client => client.Id == clientId, cancellationToken))
        {
            throw new ResourceNotFoundException("Cliente", clientId);
        }

        return await db.FormSubmissions
            .AsNoTracking()
            .Include(item => item.FormVersion)
            .Include(item => item.Signatures)
            .Where(item => item.ClientId == clientId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new FormSubmissionSummaryDto(
                item.Id,
                item.ClientId,
                item.AppointmentId,
                item.FormVersionId,
                item.FormVersion.Name,
                item.FormVersion.VersionNumber,
                item.Status,
                item.Revision,
                item.Signatures.Count > 0,
                item.FinalPdfPath != null,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.FinalizedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<FormSubmissionDto?> GetAsync(
        Guid clientId,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await QueryDetail(tracking: false).SingleOrDefaultAsync(
            item => item.Id == submissionId && item.ClientId == clientId,
            cancellationToken);
        return submission is null ? null : ToDto(submission);
    }

    public async Task<FormSubmissionDto> CreateAsync(
        Guid clientId,
        CreateFormSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(client => client.Id == clientId && client.IsActive && !client.IsDeleted, cancellationToken))
        {
            throw new ResourceNotFoundException("Cliente", clientId);
        }

        var version = await db.FormVersions
            .Include(item => item.Template)
            .SingleOrDefaultAsync(item => item.Id == request.FormVersionId, cancellationToken)
            ?? throw new ResourceNotFoundException("Versão do formulário", request.FormVersionId);
        var latestVersionId = await db.FormVersions
            .Where(item => item.FormTemplateId == version.FormTemplateId)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
        if (version.Template.Status != FormTemplateStatus.Published || latestVersionId != version.Id)
        {
            throw new BusinessRuleException("Inicie novos preenchimentos apenas pela versão publicada mais recente do formulário.");
        }

        await EnsureAppointmentAsync(clientId, request.AppointmentId, cancellationToken);
        var now = clock.UtcNow;
        var empty = NormalizeAnswers(
            FormSchemaCodec.Deserialize(version.SchemaJson),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            false,
            new HashSet<string>(StringComparer.Ordinal));
        var submission = new FormSubmission
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            AppointmentId = request.AppointmentId,
            FormVersionId = version.Id,
            FormVersion = version,
            Status = FormSubmissionStatus.Draft,
            AnswersJson = empty.Json,
            AnswersHash = empty.Hash,
            Revision = 1,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(submission);
    }

    public async Task<FormSubmissionDto> UpdateDraftAsync(
        Guid clientId,
        Guid submissionId,
        UpdateFormSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Answers);
        var submission = await FindTrackedAsync(clientId, submissionId, cancellationToken);
        EnsureStatus(submission, FormSubmissionStatus.Draft, "Somente preenchimentos em rascunho podem ser editados.");
        EnsureRevision(submission, request.Revision);
        var normalized = NormalizeAnswers(
            FormSchemaCodec.Deserialize(submission.FormVersion.SchemaJson),
            request.Answers,
            requireRequired: false,
            signatureFieldIds: new HashSet<string>(StringComparer.Ordinal));
        submission.AnswersJson = normalized.Json;
        submission.AnswersHash = normalized.Hash;
        submission.Revision++;
        submission.UpdatedByUserId = actorUserId;
        submission.UpdatedAtUtc = clock.UtcNow;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDto(submission);
    }

    public async Task DeleteDraftAsync(
        Guid clientId,
        Guid submissionId,
        int revision,
        CancellationToken cancellationToken)
    {
        var submission = await FindTrackedAsync(clientId, submissionId, cancellationToken);
        EnsureStatus(submission, FormSubmissionStatus.Draft, "Apenas rascunhos não finalizados podem ser excluídos.");
        EnsureRevision(submission, revision);
        if (submission.Signatures.Count > 0 || submission.Amendments.Count > 0)
        {
            throw new BusinessRuleException("Este rascunho contém evidências registradas e não pode ser excluído.");
        }

        db.FormSubmissions.Remove(submission);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
    }

    public async Task<FormSubmissionDto> FinalizeAsync(
        Guid clientId,
        Guid submissionId,
        FinalizeFormSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Answers);
        ArgumentNullException.ThrowIfNull(request.Signatures);
        var submission = await FindTrackedAsync(clientId, submissionId, cancellationToken);
        EnsureStatus(submission, FormSubmissionStatus.Draft, "Este preenchimento já foi finalizado e não pode ser substituído.");
        EnsureRevision(submission, request.Revision);
        var schema = FormSchemaCodec.Deserialize(submission.FormVersion.SchemaJson);
        var signatureFieldIds = SignatureFieldIds(request.Signatures);
        var normalized = NormalizeAnswers(schema, request.Answers, true, signatureFieldIds);
        var now = clock.UtcNow;
        var signatures = BuildSignatures(
            submission,
            amendmentId: null,
            request.Signatures,
            schema,
            normalized.Hash,
            actorUserId,
            now);

        submission.AnswersJson = normalized.Json;
        submission.AnswersHash = normalized.Hash;
        submission.Status = FormSubmissionStatus.Finalized;
        submission.Revision++;
        submission.UpdatedByUserId = actorUserId;
        submission.FinalizedByUserId = actorUserId;
        submission.UpdatedAtUtc = now;
        submission.FinalizedAtUtc = now;
        db.FormSignatures.AddRange(signatures);
        var archivedPdf = await pdfArchiver.ArchiveAsync(submission, schema, signatures, cancellationToken);
        submission.FinalPdfPath = archivedPdf.StoredFileName;
        submission.FinalPdfFileSizeBytes = archivedPdf.FileSizeBytes;
        submission.FinalPdfSha256 = archivedPdf.Sha256;
        try
        {
            await SaveWithConcurrencyMessageAsync(cancellationToken);
        }
        catch
        {
            pdfArchiver.Delete(archivedPdf);
            throw;
        }
        return ToDto(submission);
    }

    public async Task<FormSubmissionDto> AmendAsync(
        Guid clientId,
        Guid submissionId,
        AmendFormSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Answers);
        ArgumentNullException.ThrowIfNull(request.Signatures);
        var submission = await FindTrackedAsync(clientId, submissionId, cancellationToken);
        if (submission.Status is not (FormSubmissionStatus.Finalized or FormSubmissionStatus.Amended))
        {
            throw new BusinessRuleException("Somente preenchimentos finalizados podem receber adendos.");
        }
        EnsureRevision(submission, request.Revision);
        var schema = FormSchemaCodec.Deserialize(submission.FormVersion.SchemaJson);
        var signatureFieldIds = SignatureFieldIds(request.Signatures);
        var normalized = NormalizeAnswers(schema, request.Answers, true, signatureFieldIds);
        var previousHash = submission.Amendments.OrderByDescending(item => item.AmendmentNumber)
            .Select(item => item.AnswersHash)
            .FirstOrDefault() ?? submission.AnswersHash;
        if (previousHash == normalized.Hash)
        {
            throw new BusinessRuleException("O adendo deve registrar uma alteração nas respostas.");
        }

        var now = clock.UtcNow;
        var amendment = new FormSubmissionAmendment
        {
            Id = Guid.NewGuid(),
            FormSubmissionId = submission.Id,
            AmendmentNumber = (submission.Amendments.MaxBy(item => item.AmendmentNumber)?.AmendmentNumber ?? 0) + 1,
            Reason = request.Reason.Trim(),
            AnswersJson = normalized.Json,
            AnswersHash = normalized.Hash,
            PreviousAnswersHash = previousHash,
            CreatedByUserId = actorUserId,
            CreatedAtUtc = now
        };
        var signatures = BuildSignatures(
            submission,
            amendment.Id,
            request.Signatures,
            schema,
            normalized.Hash,
            actorUserId,
            now);
        submission.Status = FormSubmissionStatus.Amended;
        submission.Revision++;
        submission.UpdatedByUserId = actorUserId;
        submission.UpdatedAtUtc = now;
        db.FormSubmissionAmendments.Add(amendment);
        db.FormSignatures.AddRange(signatures);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDto(submission);
    }

    public async Task<FormSubmissionDto> VoidAsync(
        Guid clientId,
        Guid submissionId,
        VoidFormSubmissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var submission = await FindTrackedAsync(clientId, submissionId, cancellationToken);
        if (submission.Status == FormSubmissionStatus.Voided)
        {
            throw new BusinessRuleException("Este preenchimento já está anulado.");
        }
        EnsureRevision(submission, request.Revision);
        var now = clock.UtcNow;
        submission.Status = FormSubmissionStatus.Voided;
        submission.VoidReason = request.Reason.Trim();
        submission.VoidedByUserId = actorUserId;
        submission.VoidedAtUtc = now;
        submission.UpdatedByUserId = actorUserId;
        submission.UpdatedAtUtc = now;
        submission.Revision++;
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        return ToDto(submission);
    }

    public Task<string?> GetSignatureSvgAsync(
        Guid clientId,
        Guid submissionId,
        Guid signatureId,
        CancellationToken cancellationToken) =>
        db.FormSignatures.AsNoTracking()
            .Where(item => item.Id == signatureId && item.FormSubmissionId == submissionId && item.Submission.ClientId == clientId)
            .Select(item => item.RenderedSvg)
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<FormSubmission> QueryDetail(bool tracking)
    {
        var query = db.FormSubmissions
            .Include(item => item.FormVersion).ThenInclude(version => version.Template)
            .Include(item => item.Client)
            .Include(item => item.Amendments)
            .Include(item => item.Signatures)
            .AsSplitQuery();
        return tracking ? query : query.AsNoTracking();
    }

    private async Task<FormSubmission> FindTrackedAsync(
        Guid clientId,
        Guid submissionId,
        CancellationToken cancellationToken) =>
        await QueryDetail(tracking: true).SingleOrDefaultAsync(
            item => item.Id == submissionId && item.ClientId == clientId,
            cancellationToken)
        ?? throw new ResourceNotFoundException("Preenchimento", submissionId);

    private async Task EnsureAppointmentAsync(Guid clientId, Guid? appointmentId, CancellationToken cancellationToken)
    {
        if (appointmentId.HasValue && !await db.Appointments.AnyAsync(
                item => item.Id == appointmentId && item.ClientId == clientId,
                cancellationToken))
        {
            throw new BusinessRuleException("O atendimento informado não pertence a este cliente.");
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
            throw new ConflictException("O preenchimento mudou em outra sessão. Recarregue antes de continuar.");
        }
    }

    private static void EnsureStatus(FormSubmission submission, FormSubmissionStatus expected, string message)
    {
        if (submission.Status != expected) throw new BusinessRuleException(message);
    }

    private static void EnsureRevision(FormSubmission submission, int receivedRevision)
    {
        if (submission.Revision != receivedRevision)
        {
            throw new ConflictException("Este preenchimento foi alterado em outra sessão. Recarregue antes de continuar.");
        }
    }

    private static NormalizedAnswers NormalizeAnswers(
        FormSchemaDefinition schema,
        IReadOnlyDictionary<string, JsonElement> source,
        bool requireRequired,
        IReadOnlySet<string> signatureFieldIds)
    {
        var fields = schema.Fields.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var unknown = source.Keys.FirstOrDefault(key => !fields.ContainsKey(key));
        if (unknown is not null) throw new ArgumentException($"A resposta referencia um campo inexistente: {unknown}.");

        var result = new JsonObject();
        foreach (var field in schema.Fields)
        {
            if (field.Type is FormFieldType.Section or FormFieldType.InformationalText) continue;
            if (field.Type == FormFieldType.Signature)
            {
                if (source.ContainsKey(field.Id)) throw new ArgumentException($"A assinatura do campo '{field.Label}' deve usar a captura presencial.");
                if (requireRequired && field.Required && !signatureFieldIds.Contains(field.Id))
                {
                    throw new ArgumentException($"Colete a assinatura obrigatória: {field.Label}.");
                }
                continue;
            }

            if (!source.TryGetValue(field.Id, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (requireRequired && field.Required) throw new ArgumentException($"Responda o campo obrigatório: {field.Label}.");
                continue;
            }

            var normalized = NormalizeValue(field, value);
            if (normalized is null)
            {
                if (requireRequired && field.Required) throw new ArgumentException($"Responda o campo obrigatório: {field.Label}.");
                continue;
            }
            result[field.Id] = normalized;
        }

        var json = result.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        if (Encoding.UTF8.GetByteCount(json) > MaxAnswersBytes)
        {
            throw new ArgumentException("As respostas excedem o limite de 256 KB.");
        }
        return new NormalizedAnswers(json, Sha256(json));
    }

    private static JsonNode? NormalizeValue(FormFieldDefinition field, JsonElement value)
    {
        switch (field.Type)
        {
            case FormFieldType.ShortText:
            case FormFieldType.LongText:
                {
                    if (value.ValueKind != JsonValueKind.String) throw InvalidType(field);
                    var text = (value.GetString() ?? string.Empty).Trim();
                    var limit = field.Type == FormFieldType.ShortText ? 500 : 8000;
                    if (text.Length > limit) throw new ArgumentException($"A resposta de '{field.Label}' excede {limit} caracteres.");
                    return text.Length == 0 ? null : JsonValue.Create(text);
                }
            case FormFieldType.Number:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)) throw InvalidType(field);
                return JsonValue.Create(number);
            case FormFieldType.Date:
                {
                    if (value.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(
                        value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        throw InvalidType(field);
                    }
                    return JsonValue.Create(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
            case FormFieldType.YesNo:
            case FormFieldType.Checkbox:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw InvalidType(field);
                var flag = value.GetBoolean();
                if (field.Type == FormFieldType.Checkbox && field.Required && !flag) return null;
                return JsonValue.Create(flag);
            case FormFieldType.Dropdown:
                {
                    if (value.ValueKind != JsonValueKind.String) throw InvalidType(field);
                    var optionId = (value.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (!field.Options.Any(option => option.Id == optionId)) throw new ArgumentException($"Selecione uma opção válida em '{field.Label}'.");
                    return JsonValue.Create(optionId);
                }
            case FormFieldType.CheckboxGroup:
            case FormFieldType.MultiSelect:
                {
                    if (value.ValueKind != JsonValueKind.Array) throw InvalidType(field);
                    var allowed = field.Options.Select(option => option.Id).ToHashSet(StringComparer.Ordinal);
                    var selected = value.EnumerateArray().Select(item =>
                    {
                        if (item.ValueKind != JsonValueKind.String) throw InvalidType(field);
                        return (item.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                    }).Distinct(StringComparer.Ordinal).ToArray();
                    if (selected.Any(item => !allowed.Contains(item))) throw new ArgumentException($"Selecione somente opções válidas em '{field.Label}'.");
                    if (selected.Length == 0) return null;
                    var array = new JsonArray();
                    foreach (var item in selected) array.Add(item);
                    return array;
                }
            default:
                throw InvalidType(field);
        }
    }

    private static ArgumentException InvalidType(FormFieldDefinition field) =>
        new($"A resposta de '{field.Label}' não corresponde ao tipo {field.Type}.");

    private static HashSet<string> SignatureFieldIds(IEnumerable<FormSignatureCaptureRequest> signatures) =>
        signatures
            .Where(item => item is not null && !string.IsNullOrWhiteSpace(item.FieldId))
            .Select(item => item.FieldId.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<FormSignature> BuildSignatures(
        FormSubmission submission,
        Guid? amendmentId,
        IReadOnlyList<FormSignatureCaptureRequest> requests,
        FormSchemaDefinition schema,
        string answersHash,
        Guid actorUserId,
        DateTime capturedAtUtc)
    {
        var signatureFields = schema.Fields.Where(item => item.Type == FormFieldType.Signature)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var usedFields = new HashSet<string>(StringComparer.Ordinal);
        return requests.Select(request =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.FieldId))
            {
                throw new ArgumentException("Informe o campo associado à assinatura.");
            }
            var fieldId = request.FieldId.Trim().ToLowerInvariant();
            if (!signatureFields.ContainsKey(fieldId) || !usedFields.Add(fieldId))
            {
                throw new ArgumentException("Cada assinatura deve corresponder uma única vez a um campo de assinatura do formulário.");
            }
            if (string.IsNullOrWhiteSpace(request.SignerName) || request.SignerName.Trim().Length is < 2 or > 160)
            {
                throw new ArgumentException("Informe o nome completo do signatário, entre 2 e 160 caracteres.");
            }
            if (request.Strokes is null || request.Strokes.Count is < 1 or > MaxSignatureStrokes ||
                request.Strokes.Any(stroke => stroke is null || stroke.Count < 2 || stroke.Any(point => point is null)) ||
                request.Strokes.Sum(stroke => stroke.Count) > MaxSignaturePoints)
            {
                throw new ArgumentException("A assinatura deve possuir traços válidos e respeitar o limite de complexidade.");
            }
            if (request.CanvasWidth is < 200 or > 4096 || request.CanvasHeight is < 120 or > 4096)
            {
                throw new ArgumentException("As dimensões da área de assinatura são inválidas.");
            }
            var pointerType = request.PointerType?.Trim().ToLowerInvariant() ?? "unknown";
            if (pointerType is not ("touch" or "pen" or "mouse" or "unknown")) pointerType = "unknown";
            var normalizedStrokes = request.Strokes.Select(stroke => stroke.Select(point => new[]
            {
                Math.Round(point.X, 4),
                Math.Round(point.Y, 4)
            }).ToArray()).ToArray();
            if (normalizedStrokes.SelectMany(stroke => stroke).Any(point =>
                    !double.IsFinite(point[0]) || !double.IsFinite(point[1]) ||
                    point[0] is < 0 or > 1 || point[1] is < 0 or > 1))
            {
                throw new ArgumentException("A assinatura contém coordenadas fora da área permitida.");
            }
            var strokesJson = JsonSerializer.Serialize(normalizedStrokes);
            var signerName = request.SignerName.Trim();
            var signatureHash = Sha256(string.Join('|',
                submission.FormVersion.SchemaHash,
                answersHash,
                fieldId,
                signerName,
                SignerDeclaration,
                strokesJson,
                capturedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            return new FormSignature
            {
                Id = Guid.NewGuid(),
                FormSubmissionId = submission.Id,
                FormSubmissionAmendmentId = amendmentId,
                FieldId = fieldId,
                SignerName = signerName,
                SignerDeclaration = SignerDeclaration,
                Method = FormSignatureMethod.InPersonDrawn,
                StrokesJson = strokesJson,
                RenderedSvg = RenderSvg(normalizedStrokes),
                SignatureHash = signatureHash,
                AnswersHash = answersHash,
                SchemaHash = submission.FormVersion.SchemaHash,
                PointerType = pointerType,
                CanvasWidth = request.CanvasWidth,
                CanvasHeight = request.CanvasHeight,
                ConductedByUserId = actorUserId,
                CapturedAtUtc = capturedAtUtc
            };
        }).ToArray();
    }

    private static string RenderSvg(IReadOnlyList<double[][]> strokes)
    {
        var paths = strokes.Select(stroke => string.Join(' ', stroke.Select(point =>
            $"{(point[0] * 960).ToString("0.##", CultureInfo.InvariantCulture)},{(point[1] * 320).ToString("0.##", CultureInfo.InvariantCulture)}")));
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 960 320\" width=\"960\" height=\"320\"><rect width=\"960\" height=\"320\" fill=\"white\"/>{string.Concat(paths.Select(points => $"<polyline points=\"{points}\" fill=\"none\" stroke=\"#0f172a\" stroke-width=\"4\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"))}</svg>";
    }

    private static FormSubmissionDto ToDto(FormSubmission submission)
    {
        var amendments = submission.Amendments.OrderBy(item => item.AmendmentNumber).ToArray();
        var latest = amendments.LastOrDefault();
        var effectiveJson = latest?.AnswersJson ?? submission.AnswersJson;
        var effectiveHash = latest?.AnswersHash ?? submission.AnswersHash;
        return new FormSubmissionDto(
            submission.Id,
            submission.ClientId,
            submission.AppointmentId,
            submission.FormVersionId,
            submission.FormVersion.FormTemplateId,
            submission.FormVersion.Name,
            submission.FormVersion.Category,
            submission.FormVersion.VersionNumber,
            submission.FormVersion.SchemaHash,
            FormSchemaCodec.Deserialize(submission.FormVersion.SchemaJson),
            submission.Status,
            submission.Revision,
            ParseJson(submission.AnswersJson),
            submission.AnswersHash,
            submission.FinalPdfSha256,
            submission.FinalPdfFileSizeBytes,
            submission.FinalPdfPath is null ? null : $"/api/clients/{submission.ClientId}/form-submissions/{submission.Id}/content",
            ParseJson(effectiveJson),
            effectiveHash,
            submission.CreatedByUserId,
            submission.UpdatedByUserId,
            submission.FinalizedByUserId,
            submission.CreatedAtUtc,
            submission.UpdatedAtUtc,
            submission.FinalizedAtUtc,
            submission.VoidedByUserId,
            submission.VoidedAtUtc,
            submission.VoidReason,
            submission.Signatures.OrderBy(item => item.CapturedAtUtc).Select(item => new FormSignatureDto(
                item.Id,
                item.FieldId,
                item.SignerName,
                item.SignerDeclaration,
                item.Method,
                item.SignatureHash,
                item.AnswersHash,
                item.SchemaHash,
                item.PointerType,
                item.CapturedAtUtc,
                item.ConductedByUserId,
                item.FormSubmissionAmendmentId,
                $"/api/clients/{submission.ClientId}/form-submissions/{submission.Id}/signatures/{item.Id}/content"))
                .ToArray(),
            amendments.Select(item => new FormSubmissionAmendmentDto(
                item.Id,
                item.AmendmentNumber,
                item.Reason,
                ParseJson(item.AnswersJson),
                item.AnswersHash,
                item.PreviousAnswersHash,
                item.CreatedByUserId,
                item.CreatedAtUtc)).ToArray());
    }

    private static JsonElement ParseJson(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record NormalizedAnswers(string Json, string Hash);
}
