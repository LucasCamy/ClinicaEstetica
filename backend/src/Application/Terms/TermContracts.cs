using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Terms;

public sealed class TermLayoutDefinition
{
    public IReadOnlyList<TermFieldDefinition> Fields { get; init; } = [];
}

public sealed class TermFieldDefinition
{
    public string Id { get; init; } = string.Empty;
    public TermFieldType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string Placeholder { get; init; } = string.Empty;
    public int PageNumber { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = .3d;
    public double Height { get; init; } = .06d;
    public bool Multiline { get; init; }
}

public sealed class CreateTermTemplateRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; init; } = string.Empty;
}

public sealed class UpdateTermDraftRequest
{
    [Range(1, int.MaxValue)]
    public int DraftRevision { get; init; }

    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    public TermLayoutDefinition Layout { get; init; } = new();
}

public sealed class PublishTermRequest
{
    [Range(1, int.MaxValue)]
    public int DraftRevision { get; init; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string ChangeSummary { get; init; } = string.Empty;
}

public sealed class CreateTermSubmissionRequest
{
    public Guid TermVersionId { get; init; }
    public Guid? AppointmentId { get; init; }
}

public class UpdateTermSubmissionRequest
{
    [Range(1, int.MaxValue)]
    public int Revision { get; init; }

    public Dictionary<string, JsonElement> Values { get; init; } = new(StringComparer.Ordinal);
    public List<TermInkCaptureRequest> Ink { get; init; } = [];
}

public sealed class FinalizeTermSubmissionRequest : UpdateTermSubmissionRequest
{
}

public sealed class VoidTermSubmissionRequest
{
    [Range(1, int.MaxValue)]
    public int Revision { get; init; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class TermInkCaptureRequest
{
    [Required, StringLength(64, MinimumLength = 3)]
    public string FieldId { get; init; } = string.Empty;

    [StringLength(160)]
    public string? SignerName { get; init; }

    [StringLength(20)]
    public string PointerType { get; init; } = "unknown";

    public bool DeclarationAccepted { get; init; }

    public List<List<TermInkPointRequest>> Strokes { get; init; } = [];
}

public sealed class TermInkPointRequest
{
    [Range(0d, 1d)]
    public double X { get; init; }

    [Range(0d, 1d)]
    public double Y { get; init; }
}

public sealed record TermTemplateSummaryDto(
    Guid Id,
    string Name,
    string Description,
    TermTemplateStatus Status,
    int DraftRevision,
    bool HasDraftPdf,
    int DraftFieldCount,
    int? LatestPublishedVersionNumber,
    bool HasUnpublishedChanges,
    DateTime UpdatedAtUtc);

public sealed record TermVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    int FieldCount,
    string ChangeSummary,
    DateTime PublishedAtUtc,
    Guid PublishedByUserId,
    long PdfFileSizeBytes,
    string PdfSha256);

public sealed record TermTemplateDetailDto(
    Guid Id,
    string Name,
    string Description,
    TermTemplateStatus Status,
    int DraftRevision,
    bool HasDraftPdf,
    string? DraftPdfFileName,
    long DraftPdfFileSizeBytes,
    string? DraftPdfSha256,
    TermLayoutDefinition DraftLayout,
    bool HasUnpublishedChanges,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<TermVersionSummaryDto> Versions);

public sealed record TermVersionDto(
    Guid Id,
    Guid TermTemplateId,
    int VersionNumber,
    string Name,
    string Description,
    string PdfFileName,
    long PdfFileSizeBytes,
    string PdfSha256,
    TermLayoutDefinition Layout,
    string LayoutHash,
    string ChangeSummary,
    DateTime PublishedAtUtc,
    Guid PublishedByUserId);

public sealed record AvailableTermVersionDto(
    Guid TermTemplateId,
    Guid TermVersionId,
    int VersionNumber,
    string Name,
    string Description,
    string PdfSha256,
    TermLayoutDefinition Layout,
    string LayoutHash);

public sealed record TermInkCaptureDto(
    string FieldId,
    string? SignerName,
    string PointerType,
    bool DeclarationAccepted,
    IReadOnlyList<IReadOnlyList<TermInkPointRequest>> Strokes);

public sealed record TermSubmissionPayloadDto(
    IReadOnlyDictionary<string, JsonElement> Values,
    IReadOnlyList<TermInkCaptureDto> Ink);

public sealed record TermSubmissionSummaryDto(
    Guid Id,
    Guid ClientId,
    Guid? AppointmentId,
    Guid TermVersionId,
    string TermName,
    int VersionNumber,
    TermSubmissionStatus Status,
    int Revision,
    bool IsSigned,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FinalizedAtUtc);

public sealed record TermSubmissionDto(
    Guid Id,
    Guid ClientId,
    Guid? AppointmentId,
    Guid TermVersionId,
    Guid TermTemplateId,
    string TermName,
    int VersionNumber,
    string PdfSha256,
    TermLayoutDefinition Layout,
    string LayoutHash,
    TermSubmissionStatus Status,
    int Revision,
    TermSubmissionPayloadDto Payload,
    string ValuesHash,
    string? FinalPdfSha256,
    long? FinalPdfFileSizeBytes,
    Guid CreatedByUserId,
    Guid UpdatedByUserId,
    Guid? FinalizedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FinalizedAtUtc,
    Guid? VoidedByUserId,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    string? FinalPdfContentUrl);

public sealed record TermPdfFileSnapshot(
    string StoredFileName,
    string OriginalFileName,
    long FileSizeBytes,
    string Sha256);

public interface ITermService
{
    Task<IReadOnlyList<TermTemplateSummaryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<TermTemplateDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<TermVersionDto?> GetVersionAsync(Guid id, int versionNumber, CancellationToken cancellationToken);
    Task<TermTemplateDetailDto> CreateAsync(CreateTermTemplateRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<TermTemplateDetailDto> ReplaceDraftPdfAsync(Guid id, int draftRevision, TermPdfFileSnapshot pdf, Guid actorUserId, CancellationToken cancellationToken);
    Task<TermTemplateDetailDto> UpdateDraftAsync(Guid id, UpdateTermDraftRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<TermVersionDto> PublishAsync(Guid id, PublishTermRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<TermTemplateDetailDto> ArchiveAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AvailableTermVersionDto>> ListAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TermSubmissionSummaryDto>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<TermSubmissionDto?> GetSubmissionAsync(Guid clientId, Guid submissionId, CancellationToken cancellationToken);
    Task<TermSubmissionDto> CreateSubmissionAsync(Guid clientId, CreateTermSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<TermSubmissionDto> UpdateDraftSubmissionAsync(Guid clientId, Guid submissionId, UpdateTermSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task DeleteDraftSubmissionAsync(Guid clientId, Guid submissionId, int revision, CancellationToken cancellationToken);
    Task<TermSubmissionDto> FinalizeSubmissionAsync(Guid clientId, Guid submissionId, FinalizeTermSubmissionRequest request, TermPdfFileSnapshot pdf, Guid actorUserId, CancellationToken cancellationToken);
    Task<TermSubmissionDto> VoidSubmissionAsync(Guid clientId, Guid submissionId, VoidTermSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
}

public static class TermLayoutCodec
{
    public const int MaxFields = 100;
    public const int MaxSerializedBytes = 128 * 1024;
    private static readonly Regex StableIdPattern = new("^[a-z][a-z0-9_]{2,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static TermLayoutDefinition Normalize(TermLayoutDefinition? layout)
    {
        if (layout is null) throw new ArgumentException("Informe a definição dos campos do termo.");
        var fields = layout.Fields ?? throw new ArgumentException("A lista de campos é obrigatória.");
        if (fields.Count > MaxFields) throw new ArgumentException($"O termo pode ter no máximo {MaxFields} campos.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<TermFieldDefinition>(fields.Count);
        foreach (var field in fields)
        {
            if (field is null) throw new ArgumentException("A definição contém um campo vazio.");
            var id = (field.Id ?? string.Empty).Trim().ToLowerInvariant();
            var label = (field.Label ?? string.Empty).Trim();
            var placeholder = (field.Placeholder ?? string.Empty).Trim();
            if (!StableIdPattern.IsMatch(id) || !ids.Add(id)) throw new ArgumentException("Cada campo deve ter um identificador estável, válido e único.");
            if (label.Length is < 1 or > 200) throw new ArgumentException($"O campo '{id}' deve ter um título de até 200 caracteres.");
            if (placeholder.Length > 200) throw new ArgumentException($"O texto auxiliar de '{label}' excede o limite permitido.");
            if (field.PageNumber is < 1 or > 1000) throw new ArgumentException($"A página do campo '{label}' é inválida.");
            if (!double.IsFinite(field.X) || !double.IsFinite(field.Y) || !double.IsFinite(field.Width) || !double.IsFinite(field.Height) ||
                field.X is < 0 or >= 1 || field.Y is < 0 or >= 1 || field.Width is <= 0 or > 1 || field.Height is <= 0 or > 1 ||
                field.X + field.Width > 1 || field.Y + field.Height > 1)
            {
                throw new ArgumentException($"A área do campo '{label}' deve permanecer dentro da página.");
            }
            normalized.Add(new TermFieldDefinition
            {
                Id = id,
                Type = field.Type,
                Label = label,
                Required = field.Required,
                Placeholder = placeholder,
                PageNumber = field.PageNumber,
                X = Math.Round(field.X, 5),
                Y = Math.Round(field.Y, 5),
                Width = Math.Round(field.Width, 5),
                Height = Math.Round(field.Height, 5),
                Multiline = field.Type == TermFieldType.Text && field.Multiline
            });
        }

        var result = new TermLayoutDefinition { Fields = normalized };
        if (Encoding.UTF8.GetByteCount(Serialize(result)) > MaxSerializedBytes)
        {
            throw new ArgumentException("A definição dos campos do termo excede 128 KB.");
        }
        return result;
    }

    public static string Serialize(TermLayoutDefinition layout) => JsonSerializer.Serialize(layout, SerializerOptions);
    public static TermLayoutDefinition Deserialize(string json) => JsonSerializer.Deserialize<TermLayoutDefinition>(json, SerializerOptions)
        ?? throw new InvalidOperationException("A definição persistida do termo é inválida.");
    public static string Hash(TermLayoutDefinition layout) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(layout)))).ToLowerInvariant();
}
