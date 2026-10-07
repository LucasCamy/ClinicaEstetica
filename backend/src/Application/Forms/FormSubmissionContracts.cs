using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Forms;

public sealed record AvailableFormVersionDto(
    Guid FormTemplateId,
    Guid FormVersionId,
    int VersionNumber,
    string Name,
    string Category,
    string Description,
    FormSchemaDefinition Schema,
    string SchemaHash);

public sealed class CreateFormSubmissionRequest
{
    public Guid FormVersionId { get; init; }
    public Guid? AppointmentId { get; init; }
}

public class UpdateFormSubmissionRequest
{
    [Range(1, int.MaxValue)]
    public int Revision { get; init; }

    public Dictionary<string, JsonElement> Answers { get; init; } = new(StringComparer.Ordinal);
}

public sealed class FinalizeFormSubmissionRequest : UpdateFormSubmissionRequest
{
    public List<FormSignatureCaptureRequest> Signatures { get; init; } = [];
}

public sealed class AmendFormSubmissionRequest : UpdateFormSubmissionRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;

    public List<FormSignatureCaptureRequest> Signatures { get; init; } = [];
}

public sealed class VoidFormSubmissionRequest
{
    [Range(1, int.MaxValue)]
    public int Revision { get; init; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}

public sealed class FormSignatureCaptureRequest
{
    [Required, StringLength(64, MinimumLength = 3)]
    public string FieldId { get; init; } = string.Empty;

    [Required, StringLength(160, MinimumLength = 2)]
    public string SignerName { get; init; } = string.Empty;

    [StringLength(20)]
    public string PointerType { get; init; } = "unknown";

    [Range(200, 4096)]
    public int CanvasWidth { get; init; }

    [Range(120, 4096)]
    public int CanvasHeight { get; init; }

    public List<List<FormSignaturePointRequest>> Strokes { get; init; } = [];
}

public sealed class FormSignaturePointRequest
{
    [Range(0d, 1d)]
    public double X { get; init; }

    [Range(0d, 1d)]
    public double Y { get; init; }
}

public sealed record FormSignatureDto(
    Guid Id,
    string FieldId,
    string SignerName,
    string SignerDeclaration,
    FormSignatureMethod Method,
    string SignatureHash,
    string AnswersHash,
    string SchemaHash,
    string PointerType,
    DateTime CapturedAtUtc,
    Guid ConductedByUserId,
    Guid? AmendmentId,
    string ContentUrl);

public sealed record FormSubmissionAmendmentDto(
    Guid Id,
    int AmendmentNumber,
    string Reason,
    JsonElement Answers,
    string AnswersHash,
    string PreviousAnswersHash,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc);

public sealed record FormSubmissionSummaryDto(
    Guid Id,
    Guid ClientId,
    Guid? AppointmentId,
    Guid FormVersionId,
    string FormName,
    int VersionNumber,
    FormSubmissionStatus Status,
    int Revision,
    bool IsSigned,
    bool HasFinalPdf,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FinalizedAtUtc);

public sealed record FormSubmissionDto(
    Guid Id,
    Guid ClientId,
    Guid? AppointmentId,
    Guid FormVersionId,
    Guid FormTemplateId,
    string FormName,
    string Category,
    int VersionNumber,
    string SchemaHash,
    FormSchemaDefinition Schema,
    FormSubmissionStatus Status,
    int Revision,
    JsonElement OriginalAnswers,
    string OriginalAnswersHash,
    string? FinalPdfSha256,
    long? FinalPdfFileSizeBytes,
    string? FinalPdfContentUrl,
    JsonElement EffectiveAnswers,
    string EffectiveAnswersHash,
    Guid CreatedByUserId,
    Guid UpdatedByUserId,
    Guid? FinalizedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FinalizedAtUtc,
    Guid? VoidedByUserId,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<FormSignatureDto> Signatures,
    IReadOnlyList<FormSubmissionAmendmentDto> Amendments);

public sealed record FormFinalPdfSnapshot(
    string StoredFileName,
    long FileSizeBytes,
    string Sha256);

/// <summary>
/// Persiste o PDF original e imutável de um formulário já normalizado e assinado.
/// A implementação pertence à camada de entrega, pois controla o armazenamento privado.
/// </summary>
public interface IFormSubmissionPdfArchiver
{
    Task<FormFinalPdfSnapshot> ArchiveAsync(
        FormSubmission submission,
        FormSchemaDefinition schema,
        IReadOnlyList<FormSignature> signatures,
        CancellationToken cancellationToken);

    void Delete(FormFinalPdfSnapshot snapshot);
}

public interface IFormSubmissionService
{
    Task<IReadOnlyList<AvailableFormVersionDto>> ListAvailableAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<FormSubmissionSummaryDto>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken);
    Task<FormSubmissionDto?> GetAsync(Guid clientId, Guid submissionId, CancellationToken cancellationToken);
    Task<FormSubmissionDto> CreateAsync(Guid clientId, CreateFormSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormSubmissionDto> UpdateDraftAsync(Guid clientId, Guid submissionId, UpdateFormSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task DeleteDraftAsync(Guid clientId, Guid submissionId, int revision, CancellationToken cancellationToken);
    Task<FormSubmissionDto> FinalizeAsync(Guid clientId, Guid submissionId, FinalizeFormSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormSubmissionDto> AmendAsync(Guid clientId, Guid submissionId, AmendFormSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormSubmissionDto> VoidAsync(Guid clientId, Guid submissionId, VoidFormSubmissionRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<string?> GetSignatureSvgAsync(Guid clientId, Guid submissionId, Guid signatureId, CancellationToken cancellationToken);
}
