using System.ComponentModel.DataAnnotations;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Appointments;

public sealed record AppointmentDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid ProcedureTypeId,
    string ProcedureName,
    DateTime ScheduledDateTime,
    AppointmentStatus Status,
    decimal TotalPrice,
    decimal AmountPaid,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    string ProfessionalName,
    string? Notes,
    DateTime? CompletedAtUtc,
    DateTime CreatedAt,
    string? GoogleCalendarSyncWarning = null);

public sealed class SaveAppointmentRequest
{
    [Required]
    public Guid ClientId { get; init; }

    [Required]
    public Guid ProcedureTypeId { get; init; }

    public DateTime ScheduledDateTime { get; init; }
    public AppointmentStatus Status { get; init; } = AppointmentStatus.Scheduled;

    [Range(0, 9999999999)]
    public decimal TotalPrice { get; init; }

    [Range(0, 9999999999)]
    public decimal AmountPaid { get; init; }

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Pix;

    [StringLength(160)]
    public string ProfessionalName { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class UpdateAppointmentStatusRequest
{
    public AppointmentStatus Status { get; init; }
    public decimal? AmountPaid { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
}

public interface IAppointmentService
{
    Task<PagedResult<AppointmentDto>> ListAsync(PageRequest page, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<AppointmentDto> CreateAsync(SaveAppointmentRequest request, CancellationToken cancellationToken);
    Task<AppointmentDto> UpdateAsync(Guid id, SaveAppointmentRequest request, CancellationToken cancellationToken);
    Task<AppointmentDto> UpdateStatusAsync(Guid id, UpdateAppointmentStatusRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
