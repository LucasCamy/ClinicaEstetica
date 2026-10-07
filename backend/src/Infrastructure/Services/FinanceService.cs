using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Finance;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class FinanceService(EsteticaDbContext db, IClinicClock clock) : IFinanceService
{
    public async Task<FinanceOverviewDto> GetOverviewAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var period = ResolvePeriod(from, to);
        var fromUtc = ToUtc(period.From);
        var afterToUtc = ToUtc(period.To.AddDays(1));

        var periodAppointments = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.ScheduledDateTime >= fromUtc && appointment.ScheduledDateTime < afterToUtc)
            .ToListAsync(cancellationToken);
        var periodEntries = await db.FinancialEntries
            .AsNoTracking()
            .Where(entry => entry.EffectiveDate >= period.From && entry.EffectiveDate <= period.To)
            .ToListAsync(cancellationToken);
        var outstandingAppointments = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.ScheduledDateTime < afterToUtc &&
                                  appointment.Status != AppointmentStatus.Cancelled &&
                                  appointment.Status != AppointmentStatus.NoShow &&
                                  appointment.AmountPaid < appointment.TotalPrice)
            .ToListAsync(cancellationToken);

        var eligibleAppointments = periodAppointments
            .Where(appointment => appointment.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
            .ToList();
        var appointmentIncome = eligibleAppointments.Sum(appointment => appointment.AmountPaid);
        var manualIncome = periodEntries
            .Where(entry => entry.Type == FinancialEntryType.Income && entry.Status == FinancialEntryStatus.Settled)
            .Sum(entry => entry.Amount);
        var manualExpense = periodEntries
            .Where(entry => entry.Type == FinancialEntryType.Expense && entry.Status == FinancialEntryStatus.Settled)
            .Sum(entry => entry.Amount);
        var plannedIncome = periodEntries
            .Where(entry => entry.Type == FinancialEntryType.Income && entry.Status == FinancialEntryStatus.Planned)
            .Sum(entry => entry.Amount);
        var plannedExpense = periodEntries
            .Where(entry => entry.Type == FinancialEntryType.Expense && entry.Status == FinancialEntryStatus.Planned)
            .Sum(entry => entry.Amount);
        var receivable = outstandingAppointments.Sum(appointment => appointment.TotalPrice - appointment.AmountPaid);
        var netCash = appointmentIncome + manualIncome - manualExpense;

        var outstanding = outstandingAppointments
            .Select(appointment => ToOutstandingDto(appointment, ToLocalDate(appointment.ScheduledDateTime) < clock.LocalToday))
            .OrderByDescending(item => item.IsOverdue)
            .ThenBy(item => item.ScheduledDateTime)
            .Take(10)
            .ToList();
        var cashFlow = BuildCashFlow(period, eligibleAppointments, periodEntries);
        var expenseCategories = periodEntries
            .Where(entry => entry.Type == FinancialEntryType.Expense && entry.Status == FinancialEntryStatus.Settled)
            .GroupBy(entry => entry.Category)
            .Select(group => new FinancialCategorySummaryDto(group.Key, group.Sum(entry => entry.Amount)))
            .OrderByDescending(item => item.Amount)
            .Take(5)
            .ToList();

        return new FinanceOverviewDto(
            period.From,
            period.To,
            appointmentIncome,
            manualIncome,
            manualExpense,
            netCash,
            receivable,
            plannedIncome,
            plannedExpense,
            netCash + receivable + plannedIncome - plannedExpense,
            outstandingAppointments.Count,
            cashFlow,
            outstanding,
            expenseCategories);
    }

    public async Task<PagedResult<FinancialEntryDto>> ListEntriesAsync(
        PageRequest page,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var period = ResolvePeriod(from, to);
        var query = db.FinancialEntries
            .AsNoTracking()
            .Where(entry => entry.EffectiveDate >= period.From && entry.EffectiveDate <= period.To);
        var search = page.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLower();
            query = query.Where(entry =>
                entry.Category.ToLower().Contains(normalized) ||
                entry.Description.ToLower().Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(entry => entry.EffectiveDate)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FinancialEntryDto>(
            entries.Select(ToDto).ToArray(),
            page.SafePage,
            page.SafePageSize,
            total);
    }

    public async Task<PagedResult<FinancialLedgerItemDto>> ListLedgerAsync(
        PageRequest page,
        DateOnly? from,
        DateOnly? to,
        FinancialLedgerSource? source,
        FinancialEntryType? type,
        FinancialEntryStatus? status,
        string? category,
        CancellationToken cancellationToken)
    {
        var period = ResolvePeriod(from, to);
        var fromUtc = ToUtc(period.From);
        var afterToUtc = ToUtc(period.To.AddDays(1));
        var normalizedCategory = category?.Trim();
        var canIncludeManualEntries = source is not FinancialLedgerSource.AppointmentReceipt;
        var canIncludeAppointmentReceipts = source is not FinancialLedgerSource.ManualEntry &&
                                           (type is null or FinancialEntryType.Income) &&
                                           (status is null or FinancialEntryStatus.Settled) &&
                                           (string.IsNullOrWhiteSpace(normalizedCategory) ||
                                            string.Equals(normalizedCategory, "Atendimentos", StringComparison.OrdinalIgnoreCase));

        var items = new List<FinancialLedgerItemDto>();

        if (canIncludeManualEntries)
        {
            var entryQuery = db.FinancialEntries
                .AsNoTracking()
                .Where(entry => entry.EffectiveDate >= period.From && entry.EffectiveDate <= period.To);

            if (type.HasValue) entryQuery = entryQuery.Where(entry => entry.Type == type.Value);
            if (status.HasValue) entryQuery = entryQuery.Where(entry => entry.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(normalizedCategory))
            {
                var normalized = normalizedCategory.ToLower();
                entryQuery = entryQuery.Where(entry => entry.Category.ToLower() == normalized);
            }

            var entries = await entryQuery.ToListAsync(cancellationToken);
            items.AddRange(entries.Select(entry => new FinancialLedgerItemDto(
                entry.Id,
                FinancialLedgerSource.ManualEntry,
                entry.Type,
                entry.Status,
                entry.Category,
                entry.Description,
                entry.Amount,
                entry.PaymentMethod,
                entry.EffectiveDate,
                entry.Notes,
                entry.CreatedAtUtc,
                null,
                null,
                null,
                null,
                null,
                null)));
        }

        if (canIncludeAppointmentReceipts)
        {
            var appointments = await db.Appointments
                .AsNoTracking()
                .Where(appointment =>
                    appointment.ScheduledDateTime >= fromUtc &&
                    appointment.ScheduledDateTime < afterToUtc &&
                    appointment.Status != AppointmentStatus.Cancelled &&
                    appointment.Status != AppointmentStatus.NoShow &&
                    appointment.AmountPaid > 0)
                .ToListAsync(cancellationToken);

            items.AddRange(appointments.Select(appointment => new FinancialLedgerItemDto(
                appointment.Id,
                FinancialLedgerSource.AppointmentReceipt,
                FinancialEntryType.Income,
                FinancialEntryStatus.Settled,
                "Atendimentos",
                $"{appointment.ProcedureName} — {appointment.ClientName}",
                appointment.AmountPaid,
                appointment.PaymentMethod,
                ToLocalDate(appointment.ScheduledDateTime),
                "Recebimento informado no atendimento.",
                appointment.CreatedAt,
                appointment.Id,
                appointment.ClientId,
                appointment.ClientName,
                appointment.ProcedureName,
                appointment.Status,
                appointment.PaymentStatus)));
        }

        var search = page.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.ToLower();
            items = items.Where(item =>
                    item.Category.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    (item.ClientName?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.ProcedureName?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        var orderedItems = items
            .OrderByDescending(item => item.EffectiveDate)
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToList();

        return new PagedResult<FinancialLedgerItemDto>(
            orderedItems.Skip(page.Skip).Take(page.SafePageSize).ToArray(),
            page.SafePage,
            page.SafePageSize,
            orderedItems.Count);
    }

    public Task<FinancialEntryDto> CreateEntryAsync(
        SaveFinancialEntryRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        SaveAsync(null, request, actorUserId, cancellationToken);

    public Task<FinancialEntryDto> UpdateEntryAsync(
        Guid id,
        SaveFinancialEntryRequest request,
        CancellationToken cancellationToken) =>
        SaveAsync(id, request, null, cancellationToken);

    public async Task<FinancialEntryDto> SettleEntryAsync(
        Guid id,
        SettleFinancialEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EffectiveDate == default)
        {
            throw new BusinessRuleException("Informe a data de realização do lançamento.");
        }

        var entry = await FindTrackedAsync(id, cancellationToken);
        EnsureNotCancelled(entry);
        entry.Status = FinancialEntryStatus.Settled;
        entry.EffectiveDate = request.EffectiveDate;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task CancelEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await FindTrackedAsync(id, cancellationToken);
        if (entry.Status == FinancialEntryStatus.Cancelled) return;

        entry.Status = FinancialEntryStatus.Cancelled;
        entry.CancelledAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<FinancialEntryDto> SaveAsync(
        Guid? id,
        SaveFinancialEntryRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        if (request.EffectiveDate == default)
        {
            throw new BusinessRuleException("Informe a data do lançamento.");
        }

        if (request.Status == FinancialEntryStatus.Cancelled)
        {
            throw new BusinessRuleException("Use a ação de cancelar para manter a trilha do lançamento.");
        }

        var entry = id.HasValue
            ? await FindTrackedAsync(id.Value, cancellationToken)
            : new FinancialEntry { Id = Guid.NewGuid(), CreatedAtUtc = clock.UtcNow, CreatedByUserId = actorUserId ?? Guid.Empty };
        EnsureNotCancelled(entry);

        entry.Type = request.Type;
        entry.Status = request.Status;
        entry.Category = request.Category.Trim();
        entry.Description = request.Description.Trim();
        entry.Amount = request.Amount;
        entry.PaymentMethod = request.PaymentMethod;
        entry.EffectiveDate = request.EffectiveDate;
        entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        if (!id.HasValue) db.FinancialEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    private async Task<FinancialEntry> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.FinancialEntries.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException("Lançamento financeiro", id);

    private static void EnsureNotCancelled(FinancialEntry entry)
    {
        if (entry.Status == FinancialEntryStatus.Cancelled)
        {
            throw new BusinessRuleException("Lançamentos cancelados são preservados para auditoria e não podem ser alterados.");
        }
    }

    private IReadOnlyList<FinancialCashFlowDayDto> BuildCashFlow(
        FinancePeriod period,
        IReadOnlyList<Appointment> appointments,
        IReadOnlyList<FinancialEntry> entries) =>
        Enumerable.Range(0, period.To.DayNumber - period.From.DayNumber + 1)
            .Select(offset =>
            {
                var day = period.From.AddDays(offset);
                var appointmentIncome = appointments
                    .Where(appointment => ToLocalDate(appointment.ScheduledDateTime) == day)
                    .Sum(appointment => appointment.AmountPaid);
                var manualIncome = entries
                    .Where(entry => entry.EffectiveDate == day && entry.Type == FinancialEntryType.Income && entry.Status == FinancialEntryStatus.Settled)
                    .Sum(entry => entry.Amount);
                var expense = entries
                    .Where(entry => entry.EffectiveDate == day && entry.Type == FinancialEntryType.Expense && entry.Status == FinancialEntryStatus.Settled)
                    .Sum(entry => entry.Amount);
                return new FinancialCashFlowDayDto(day, appointmentIncome + manualIncome, expense);
            })
            .ToList();

    private FinancePeriod ResolvePeriod(DateOnly? from, DateOnly? to)
    {
        var today = clock.LocalToday;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);
        if (end < start || end.DayNumber - start.DayNumber > 365)
        {
            throw new BusinessRuleException("Escolha um período de até 366 dias, com data inicial anterior à final.");
        }

        return new FinancePeriod(start, end);
    }

    private DateOnly ToLocalDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            clock.TimeZone));

    private DateTime ToUtc(DateOnly localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
            clock.TimeZone);

    private static FinancialEntryDto ToDto(FinancialEntry entry) =>
        new(
            entry.Id,
            entry.Type,
            entry.Status,
            entry.Category,
            entry.Description,
            entry.Amount,
            entry.PaymentMethod,
            entry.EffectiveDate,
            entry.Notes,
            entry.CreatedAtUtc);

    private static OutstandingReceivableDto ToOutstandingDto(Appointment appointment, bool isOverdue) =>
        new(
            appointment.Id,
            appointment.ClientId,
            appointment.ClientName,
            appointment.ProcedureName,
            appointment.ScheduledDateTime,
            appointment.Status,
            appointment.TotalPrice,
            appointment.AmountPaid,
            appointment.TotalPrice - appointment.AmountPaid,
            appointment.PaymentMethod,
            appointment.PaymentStatus,
            isOverdue);

    private sealed record FinancePeriod(DateOnly From, DateOnly To);
}
