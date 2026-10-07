using System.ComponentModel.DataAnnotations;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Reports;

public sealed record DashboardDto(
    int TotalClients,
    int TodayAppointments,
    decimal MonthlyRevenue,
    int PendingLeads,
    int ActiveProcedures,
    int PublishedForms,
    int FinalizedFormsThisMonth,
    int PublishedTerms,
    int FinalizedTermsThisMonth,
    decimal MonthlyExpenses,
    int AppointmentsThisMonth,
    int ScheduledAppointmentsThisMonth,
    int ConfirmedAppointmentsThisMonth,
    int InProgressAppointmentsThisMonth,
    int CompletedAppointmentsThisMonth,
    int CancelledAppointmentsThisMonth,
    int NoShowAppointmentsThisMonth,
    IReadOnlyList<DashboardScheduleDayDto> UpcomingSchedule,
    IReadOnlyList<DashboardBirthdayDto> UpcomingBirthdays,
    IReadOnlyList<DashboardProcedureRankingDto> TopProcedures,
    int DueReturnsCount,
    IReadOnlyList<DashboardReturnAlertDto> DueReturns);

public sealed record DashboardScheduleDayDto(
    DateOnly Date,
    int Scheduled,
    int Confirmed,
    int InProgress,
    int Completed,
    int Cancelled,
    int NoShow);

public sealed record DashboardBirthdayDto(
    Guid ClientId,
    string Name,
    DateOnly BirthDate,
    int TurningAge,
    int DaysUntil);

public sealed record DashboardProcedureRankingDto(
    string ProcedureName,
    int Appointments,
    decimal AmountReceived);

public sealed record DashboardReturnAlertDto(
    Guid ClientId,
    string ClientName,
    string ProcedureName,
    DateOnly LastCompletedDate,
    DateOnly ReturnDueDate,
    int DaysUntil);

public static class CustomReportFields
{
    public const string ClientName = "clientName";
    public const string ClientPhone = "clientPhone";
    public const string ClientCity = "clientCity";
    public const string ClientBirthDate = "clientBirthDate";
    public const string ClientSource = "clientSource";
    public const string ProcedureName = "procedureName";
    public const string ScheduledDate = "scheduledDate";
    public const string CompletedDate = "completedDate";
    public const string AppointmentStatus = "appointmentStatus";
    public const string PaymentStatus = "paymentStatus";
    public const string PaymentMethod = "paymentMethod";
    public const string TotalPrice = "totalPrice";
    public const string AmountPaid = "amountPaid";
    public const string OutstandingAmount = "outstandingAmount";
    public const string ProfessionalName = "professionalName";
    public const string LastCompletedProcedure = "lastCompletedProcedure";
    public const string LastCompletedDate = "lastCompletedDate";
    public const string ReturnDueDate = "returnDueDate";
    public const string ReturnStatus = "returnStatus";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        ClientName, ClientPhone, ClientCity, ClientBirthDate, ClientSource,
        ProcedureName, ScheduledDate, CompletedDate, AppointmentStatus,
        PaymentStatus, PaymentMethod, TotalPrice, AmountPaid, OutstandingAmount,
        ProfessionalName, LastCompletedProcedure, LastCompletedDate, ReturnDueDate, ReturnStatus
    };
}

public sealed class CustomReportRequest
{
    [Required, MinLength(1), MaxLength(19)]
    public IReadOnlyList<string> Fields { get; init; } = [];

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? ProcedureTypeId { get; init; }
    public IReadOnlyList<AppointmentStatus> AppointmentStatuses { get; init; } = [];
    public IReadOnlyList<PaymentStatus> PaymentStatuses { get; init; } = [];
    public int? BirthdayMonth { get; init; }
    public string? Search { get; init; }
    public bool? OnlyReturnsDue { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    [Range(1, 200)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}

public sealed record CustomReportColumnDto(string Key, string Label);

public sealed record CustomReportRowDto(
    Guid AppointmentId,
    Guid ClientId,
    IReadOnlyDictionary<string, string?> Values);

public sealed record CustomReportResultDto(
    IReadOnlyList<CustomReportColumnDto> Columns,
    IReadOnlyList<CustomReportRowDto> Rows,
    int TotalCount);

public interface IReportService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<CustomReportResultDto> RunCustomReportAsync(CustomReportRequest request, int? exportLimit, CancellationToken cancellationToken);
}
