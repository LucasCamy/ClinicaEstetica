using System.ComponentModel.DataAnnotations;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Clients;

public sealed record ClientDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Cpf,
    string Rg,
    DateOnly? BirthDate,
    string Profession,
    LeadSource Source,
    string? Address,
    string? City,
    string? State,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt);

public sealed record ClinicalClientDto(
    ClientDto Client,
    IReadOnlyList<MedicalRecordDto> MedicalRecords,
    IReadOnlyList<ClientPhotoDto> Photos,
    IReadOnlyList<ClientDocumentDto> Documents);

public sealed record ProcedureCountDto(string ProcedureName, int Count);

public sealed record ClientSummaryDto(
    Guid ClientId,
    int TotalAppointments,
    int CompletedAppointments,
    int UpcomingAppointments,
    DateTime? NextAppointmentAt,
    int ClinicalRecordsCount,
    DateTime? LastClinicalRecordAt,
    IReadOnlyList<ProcedureCountDto> MostPerformedProcedures,
    int DocumentsCount,
    int PhotosCount,
    int DraftFormsCount,
    int FinalizedFormsCount,
    int SignedFormsCount,
    decimal TotalContracted,
    decimal TotalPaid,
    decimal OutstandingBalance);

public sealed record MedicalRecordDto(
    Guid Id,
    Guid ClientId,
    Guid? AppointmentId,
    string ProcedureName,
    string TreatedArea,
    string ParametersUsed,
    string ClinicalNotes,
    string PostCareInstructions,
    DateTime SessionDate,
    DateTime CreatedAt);

public sealed record ClientPhotoDto(
    Guid Id,
    Guid ClientId,
    PhotoType Type,
    string ProcedureName,
    bool IsPublicForWebsite,
    bool ConsentGiven,
    DateTime CreatedAt,
    long OriginalFileSizeBytes,
    long FileSizeBytes,
    int Width,
    int Height,
    string ContentUrl);

public sealed record ClientDocumentDto(
    Guid Id,
    Guid ClientId,
    string FileName,
    string DocumentType,
    DateTime UploadedAt,
    string ContentType,
    long FileSizeBytes,
    string ContentUrl);

public sealed class SaveClientRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 8)]
    public string Phone { get; init; } = string.Empty;

    [StringLength(14)]
    public string Cpf { get; init; } = string.Empty;

    [StringLength(30)]
    public string Rg { get; init; } = string.Empty;

    public DateOnly? BirthDate { get; init; }

    [StringLength(120)]
    public string Profession { get; init; } = string.Empty;

    public LeadSource Source { get; init; } = LeadSource.Instagram;

    [StringLength(240)]
    public string? Address { get; init; }

    [StringLength(120)]
    public string? City { get; init; }

    [StringLength(2)]
    public string? State { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class CreateMedicalRecordRequest
{
    public Guid? AppointmentId { get; init; }

    [Required, StringLength(160)]
    public string ProcedureName { get; init; } = string.Empty;

    [Required, StringLength(1000)]
    public string TreatedArea { get; init; } = string.Empty;

    [Required, StringLength(2000)]
    public string ParametersUsed { get; init; } = string.Empty;

    [StringLength(4000)]
    public string ClinicalNotes { get; init; } = string.Empty;

    [StringLength(4000)]
    public string PostCareInstructions { get; init; } = string.Empty;

    public DateTime? SessionDate { get; init; }
}

public interface IClientService
{
    Task<PagedResult<ClientDto>> ListAsync(PageRequest page, CancellationToken cancellationToken);
    Task<ClientDto?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ClinicalClientDto?> GetClinicalAsync(Guid id, CancellationToken cancellationToken);
    Task<ClientSummaryDto> GetSummaryAsync(Guid id, CancellationToken cancellationToken);
    Task<ClientDto> CreateAsync(SaveClientRequest request, CancellationToken cancellationToken);
    Task<ClientDto> UpdateAsync(Guid id, SaveClientRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<MedicalRecordDto> CreateMedicalRecordAsync(Guid clientId, CreateMedicalRecordRequest request, CancellationToken cancellationToken);
}
