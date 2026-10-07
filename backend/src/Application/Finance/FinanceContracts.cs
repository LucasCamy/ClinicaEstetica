using System.ComponentModel.DataAnnotations;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Finance;

public sealed record FinancialEntryDto(
    Guid Id,
    FinancialEntryType Type,
    FinancialEntryStatus Status,
    string Category,
    string Description,
    decimal Amount,
    PaymentMethod PaymentMethod,
    DateOnly EffectiveDate,
    string? Notes,
    DateTime CreatedAtUtc);

public sealed record FinancialLedgerItemDto(
    Guid Id,
    FinancialLedgerSource Source,
    FinancialEntryType Type,
    FinancialEntryStatus Status,
    string Category,
    string Description,
    decimal Amount,
    PaymentMethod PaymentMethod,
    DateOnly EffectiveDate,
    string? Notes,
    DateTime CreatedAtUtc,
    Guid? AppointmentId,
    Guid? ClientId,
    string? ClientName,
    string? ProcedureName,
    AppointmentStatus? AppointmentStatus,
    PaymentStatus? PaymentStatus);

public sealed class SaveFinancialEntryRequest
{
    public FinancialEntryType Type { get; init; }
    public FinancialEntryStatus Status { get; init; } = FinancialEntryStatus.Settled;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Category { get; init; } = string.Empty;

    [Required, StringLength(240, MinimumLength = 2)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 9999999999)]
    public decimal Amount { get; init; }

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Pix;
    public DateOnly EffectiveDate { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }
}

public sealed class SettleFinancialEntryRequest
{
    public DateOnly EffectiveDate { get; init; }
}

public sealed record FinancialCashFlowDayDto(DateOnly Date, decimal Income, decimal Expense);

public sealed record FinancialCategorySummaryDto(string Category, decimal Amount);

public sealed record OutstandingReceivableDto(
    Guid AppointmentId,
    Guid ClientId,
    string ClientName,
    string ProcedureName,
    DateTime ScheduledDateTime,
    AppointmentStatus Status,
    decimal TotalPrice,
    decimal AmountPaid,
    decimal OutstandingAmount,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    bool IsOverdue);

public sealed record FinanceOverviewDto(
    DateOnly From,
    DateOnly To,
    decimal AppointmentIncome,
    decimal ManualIncome,
    decimal ManualExpense,
    decimal NetCash,
    decimal Receivable,
    decimal PlannedIncome,
    decimal PlannedExpense,
    decimal ProjectedBalance,
    int PendingReceivables,
    IReadOnlyList<FinancialCashFlowDayDto> CashFlow,
    IReadOnlyList<OutstandingReceivableDto> OutstandingReceivables,
    IReadOnlyList<FinancialCategorySummaryDto> ExpenseCategories);

public interface IFinanceService
{
    Task<FinanceOverviewDto> GetOverviewAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<PagedResult<FinancialEntryDto>> ListEntriesAsync(PageRequest page, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<PagedResult<FinancialLedgerItemDto>> ListLedgerAsync(
        PageRequest page,
        DateOnly? from,
        DateOnly? to,
        FinancialLedgerSource? source,
        FinancialEntryType? type,
        FinancialEntryStatus? status,
        string? category,
        CancellationToken cancellationToken);
    Task<FinancialEntryDto> CreateEntryAsync(SaveFinancialEntryRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<FinancialEntryDto> UpdateEntryAsync(Guid id, SaveFinancialEntryRequest request, CancellationToken cancellationToken);
    Task<FinancialEntryDto> SettleEntryAsync(Guid id, SettleFinancialEntryRequest request, CancellationToken cancellationToken);
    Task CancelEntryAsync(Guid id, CancellationToken cancellationToken);
}
