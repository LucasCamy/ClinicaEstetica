using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Forms;

public sealed class FormSchemaDefinition
{
    public IReadOnlyList<FormFieldDefinition> Fields { get; init; } = [];
}

public sealed class FormFieldDefinition
{
    public string Id { get; init; } = string.Empty;
    public FormFieldType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string Placeholder { get; init; } = string.Empty;
    public IReadOnlyList<FormFieldOption> Options { get; init; } = [];
}

public sealed class FormFieldOption
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class CreateFormTemplateRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Category { get; init; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; init; } = string.Empty;
}

public sealed class UpdateFormDraftRequest
{
    [Range(1, int.MaxValue)]
    public int DraftRevision { get; init; }

    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Category { get; init; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    public FormSchemaDefinition Schema { get; init; } = new();
}

public sealed class PublishFormRequest
{
    [Range(1, int.MaxValue)]
    public int DraftRevision { get; init; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string ChangeSummary { get; init; } = string.Empty;
}

public sealed class DuplicateFormRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
}

public sealed record FormSummaryDto(
    Guid Id,
    string Name,
    string Category,
    string Description,
    FormTemplateStatus Status,
    int DraftRevision,
    int DraftFieldCount,
    int? LatestPublishedVersionNumber,
    bool HasUnpublishedChanges,
    DateTime UpdatedAtUtc);

public sealed record FormVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    int FieldCount,
    string ChangeSummary,
    DateTime PublishedAtUtc,
    Guid PublishedByUserId);

public sealed record FormVersionDto(
    Guid Id,
    Guid FormTemplateId,
    int VersionNumber,
    string Name,
    string Category,
    string Description,
    FormSchemaDefinition Schema,
    string SchemaHash,
    string ChangeSummary,
    DateTime PublishedAtUtc,
    Guid PublishedByUserId);

public sealed record FormDetailDto(
    Guid Id,
    string Name,
    string Category,
    string Description,
    FormTemplateStatus Status,
    int DraftRevision,
    FormSchemaDefinition DraftSchema,
    bool HasUnpublishedChanges,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<FormVersionSummaryDto> Versions);

public interface IFormService
{
    Task<IReadOnlyList<FormSummaryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<FormDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<FormVersionDto?> GetVersionAsync(Guid id, int versionNumber, CancellationToken cancellationToken);
    Task<FormDetailDto> CreateAsync(CreateFormTemplateRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormDetailDto> UpdateDraftAsync(Guid id, UpdateFormDraftRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormVersionDto> PublishAsync(Guid id, PublishFormRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormDetailDto> DuplicateAsync(Guid id, DuplicateFormRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FormDetailDto> ArchiveAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken);
}

public static class FormSchemaCodec
{
    public const int MaxFields = 100;
    public const int MaxOptionsPerField = 50;
    public const int MaxSerializedBytes = 128 * 1024;

    private static readonly Regex StableIdPattern = new(
        "^[a-z][a-z0-9_]{2,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static FormSchemaDefinition Normalize(FormSchemaDefinition? schema, bool requireFillableField)
    {
        if (schema is null) throw new ArgumentException("Informe a definição dos campos do formulário.");
        var sourceFields = schema.Fields
            ?? throw new ArgumentException("A lista de campos do formulário é obrigatória.");
        if (sourceFields.Count > MaxFields)
        {
            throw new ArgumentException($"O formulário pode ter no máximo {MaxFields} campos.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalizedFields = new List<FormFieldDefinition>(sourceFields.Count);
        foreach (var field in sourceFields)
        {
            if (field is null) throw new ArgumentException("A definição contém um campo vazio.");
            var id = (field.Id ?? string.Empty).Trim().ToLowerInvariant();
            if (!StableIdPattern.IsMatch(id) || !ids.Add(id))
            {
                throw new ArgumentException("Cada campo deve possuir um identificador estável, válido e único.");
            }

            var label = (field.Label ?? string.Empty).Trim();
            if (label.Length is < 1 or > 200)
            {
                throw new ArgumentException($"O campo '{id}' deve possuir um título com até 200 caracteres.");
            }
            var description = (field.Description ?? string.Empty).Trim();
            var placeholder = (field.Placeholder ?? string.Empty).Trim();
            if (description.Length > 1000 || placeholder.Length > 200)
            {
                throw new ArgumentException($"Os textos auxiliares do campo '{label}' excedem o limite permitido.");
            }

            var needsOptions = field.Type is FormFieldType.CheckboxGroup or FormFieldType.Dropdown or FormFieldType.MultiSelect;
            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            var optionLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedOptions = (field.Options ?? []).Select(option =>
            {
                if (option is null) throw new ArgumentException($"O campo '{label}' contém uma opção vazia.");
                var optionId = (option.Id ?? string.Empty).Trim().ToLowerInvariant();
                var optionLabel = (option.Label ?? string.Empty).Trim();
                if (!StableIdPattern.IsMatch(optionId) || !optionIds.Add(optionId) ||
                    optionLabel.Length is < 1 or > 160 || !optionLabels.Add(optionLabel))
                {
                    throw new ArgumentException($"As opções do campo '{label}' devem possuir identificadores e textos válidos e únicos.");
                }
                return new FormFieldOption { Id = optionId, Label = optionLabel };
            }).ToArray();

            if (normalizedOptions.Length > MaxOptionsPerField || needsOptions && normalizedOptions.Length < 1)
            {
                throw new ArgumentException(
                    $"O campo '{label}' deve possuir entre 1 e {MaxOptionsPerField} opções.");
            }
            if (!needsOptions && normalizedOptions.Length > 0)
            {
                throw new ArgumentException($"O tipo do campo '{label}' não aceita uma lista de opções.");
            }

            var isDisplayOnly = field.Type is FormFieldType.Section or FormFieldType.InformationalText;
            normalizedFields.Add(new FormFieldDefinition
            {
                Id = id,
                Type = field.Type,
                Label = label,
                Description = description,
                Required = !isDisplayOnly && field.Required,
                Placeholder = placeholder,
                Options = normalizedOptions
            });
        }

        if (requireFillableField && normalizedFields.All(field =>
                field.Type is FormFieldType.Section or FormFieldType.InformationalText))
        {
            throw new ArgumentException("Inclua ao menos um campo preenchível antes de publicar.");
        }

        var normalized = new FormSchemaDefinition { Fields = normalizedFields };
        if (Encoding.UTF8.GetByteCount(Serialize(normalized)) > MaxSerializedBytes)
        {
            throw new ArgumentException("A definição do formulário excede o limite de 128 KB.");
        }
        return normalized;
    }

    public static string Serialize(FormSchemaDefinition schema) =>
        JsonSerializer.Serialize(schema, SerializerOptions);

    public static FormSchemaDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize<FormSchemaDefinition>(json, SerializerOptions)
        ?? throw new InvalidOperationException("A definição persistida do formulário é inválida.");

    public static string Hash(FormSchemaDefinition schema) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(schema)))).ToLowerInvariant();
}
