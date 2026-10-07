using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Reports;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class ReportService(EsteticaDbContext db, IClinicClock clock) : IReportService
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var today = clock.LocalToday;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var nextMonth = monthStart.AddMonths(1);

        var todayStartUtc = ToUtc(today);
        var tomorrowStartUtc = ToUtc(tomorrow);
        var monthStartUtc = ToUtc(monthStart);
        var nextMonthStartUtc = ToUtc(nextMonth);
        var nextSevenDays = today.AddDays(7);
        var nextSevenDaysUtc = ToUtc(nextSevenDays);

        var totalClients = await db.Clients.CountAsync(cancellationToken);
        var todayAppointments = await db.Appointments.CountAsync(
            appointment => appointment.ScheduledDateTime >= todayStartUtc &&
                           appointment.ScheduledDateTime < tomorrowStartUtc &&
                           appointment.Status != AppointmentStatus.Cancelled &&
                           appointment.Status != AppointmentStatus.NoShow,
            cancellationToken);
        var monthlyAppointments = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.ScheduledDateTime >= monthStartUtc &&
                                  appointment.ScheduledDateTime < nextMonthStartUtc)
            .ToListAsync(cancellationToken);
        var upcomingAppointments = await db.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.ScheduledDateTime >= todayStartUtc &&
                                  appointment.ScheduledDateTime < nextSevenDaysUtc)
            .ToListAsync(cancellationToken);
        var activeProcedures = await db.ProcedureTypes.CountAsync(procedure => procedure.IsActive, cancellationToken);
        var publishedForms = await db.FormTemplates.CountAsync(
            template => template.Status == FormTemplateStatus.Published,
            cancellationToken);
        var finalizedFormsThisMonth = await db.FormSubmissions.CountAsync(
            submission => (submission.Status == FormSubmissionStatus.Finalized || submission.Status == FormSubmissionStatus.Amended) &&
                          submission.FinalizedAtUtc >= monthStartUtc &&
                          submission.FinalizedAtUtc < nextMonthStartUtc,
            cancellationToken);
        var publishedTerms = await db.TermTemplates.CountAsync(
            template => template.Status == TermTemplateStatus.Published,
            cancellationToken);
        var finalizedTermsThisMonth = await db.TermSubmissions.CountAsync(
            submission => submission.Status == TermSubmissionStatus.Finalized &&
                          submission.FinalizedAtUtc >= monthStartUtc &&
                          submission.FinalizedAtUtc < nextMonthStartUtc,
            cancellationToken);
        var monthlyExpenses = await db.FinancialEntries
            .Where(entry => entry.Type == FinancialEntryType.Expense &&
                            entry.Status == FinancialEntryStatus.Settled &&
                            entry.EffectiveDate >= monthStart &&
                            entry.EffectiveDate < nextMonth)
            .SumAsync(entry => (decimal?)entry.Amount, cancellationToken) ?? 0m;
        var pendingLeads = await db.Leads.CountAsync(lead => lead.Status == "Novo", cancellationToken);
        var birthdayClients = await db.Clients
            .AsNoTracking()
            .Where(client => client.BirthDate.HasValue)
            .Select(client => new ClientBirthday(client.Id, client.Name, client.BirthDate!.Value))
            .ToListAsync(cancellationToken);
        var returnCandidates = await (
                from appointment in db.Appointments.AsNoTracking()
                join client in db.Clients.AsNoTracking() on appointment.ClientId equals client.Id
                join procedure in db.ProcedureTypes.AsNoTracking() on appointment.ProcedureTypeId equals procedure.Id
                where appointment.Status == AppointmentStatus.Completed && procedure.RecommendedReturnDays.HasValue
                select new ReturnCandidate(
                    appointment.ClientId,
                    client.Name,
                    appointment.ProcedureTypeId,
                    appointment.ProcedureName,
                    appointment.CompletedAtUtc ?? appointment.ScheduledDateTime,
                    procedure.RecommendedReturnDays!.Value))
            .ToListAsync(cancellationToken);

        var monthlyRevenue = monthlyAppointments.Sum(appointment => appointment.AmountPaid);
        var upcomingSchedule = BuildUpcomingSchedule(today, upcomingAppointments);
        var upcomingBirthdays = BuildUpcomingBirthdays(today, birthdayClients);
        var dueReturns = BuildDueReturns(today, returnCandidates);
        var topProcedures = monthlyAppointments
            .Where(appointment => appointment.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow))
            .GroupBy(appointment => appointment.ProcedureName)
            .Select(group => new DashboardProcedureRankingDto(
                group.Key,
                group.Count(),
                group.Sum(appointment => appointment.AmountPaid)))
            .OrderByDescending(item => item.Appointments)
            .ThenBy(item => item.ProcedureName)
            .Take(5)
            .ToList();

        return new DashboardDto(
            totalClients,
            todayAppointments,
            monthlyRevenue,
            pendingLeads,
            activeProcedures,
            publishedForms,
            finalizedFormsThisMonth,
            publishedTerms,
            finalizedTermsThisMonth,
            monthlyExpenses,
            monthlyAppointments.Count,
            CountStatus(monthlyAppointments, AppointmentStatus.Scheduled),
            CountStatus(monthlyAppointments, AppointmentStatus.Confirmed),
            CountStatus(monthlyAppointments, AppointmentStatus.InProgress),
            CountStatus(monthlyAppointments, AppointmentStatus.Completed),
            CountStatus(monthlyAppointments, AppointmentStatus.Cancelled),
            CountStatus(monthlyAppointments, AppointmentStatus.NoShow),
            upcomingSchedule,
            upcomingBirthdays,
            topProcedures,
            dueReturns.Count,
            dueReturns.Take(6).ToArray());
    }

    public async Task<CustomReportResultDto> RunCustomReportAsync(
        CustomReportRequest request,
        int? exportLimit,
        CancellationToken cancellationToken)
    {
        ValidateCustomReportRequest(request);
        var fields = request.Fields.Distinct(StringComparer.Ordinal).ToArray();
        var query = from appointment in db.Appointments.AsNoTracking()
                    join client in db.Clients.AsNoTracking() on appointment.ClientId equals client.Id
                    join procedure in db.ProcedureTypes.AsNoTracking() on appointment.ProcedureTypeId equals procedure.Id
                    select new ReportQueryRow
                    {
                        Appointment = appointment,
                        Client = client,
                        Procedure = procedure
                    };

        if (request.ProcedureTypeId.HasValue)
        {
            query = query.Where(item => item.Appointment.ProcedureTypeId == request.ProcedureTypeId.Value);
        }

        if (request.AppointmentStatuses.Count > 0)
        {
            query = query.Where(item => request.AppointmentStatuses.Contains(item.Appointment.Status));
        }

        if (request.PaymentStatuses.Count > 0)
        {
            query = query.Where(item => request.PaymentStatuses.Contains(item.Appointment.PaymentStatus));
        }

        if (request.FromDate.HasValue)
        {
            var fromUtc = ToUtc(request.FromDate.Value);
            query = query.Where(item => (item.Appointment.CompletedAtUtc ?? item.Appointment.ScheduledDateTime) >= fromUtc);
        }

        if (request.ToDate.HasValue)
        {
            var untilUtc = ToUtc(request.ToDate.Value.AddDays(1));
            query = query.Where(item => (item.Appointment.CompletedAtUtc ?? item.Appointment.ScheduledDateTime) < untilUtc);
        }

        if (request.BirthdayMonth.HasValue)
        {
            query = query.Where(item => item.Client.BirthDate.HasValue && item.Client.BirthDate.Value.Month == request.BirthdayMonth.Value);
        }

        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLowerInvariant();
            query = query.Where(item =>
                item.Client.Name.ToLower().Contains(normalized) ||
                item.Client.Phone.Contains(search) ||
                item.Appointment.ProcedureName.ToLower().Contains(normalized));
        }

        query = ApplySort(query, request.SortBy, request.SortDescending);
        var totalCount = await query.CountAsync(cancellationToken);
        var limit = exportLimit ?? request.PageSize;
        var skip = exportLimit.HasValue ? 0 : (request.Page - 1) * request.PageSize;
        var rows = await query
            .Skip(skip)
            .Take(limit)
            .Select(item => new ReportSourceRow(
                item.Appointment.Id,
                item.Appointment.ClientId,
                item.Client.Name,
                item.Client.Phone,
                item.Client.City,
                item.Client.BirthDate,
                item.Client.Source.ToString(),
                item.Appointment.ProcedureTypeId,
                item.Appointment.ProcedureName,
                item.Appointment.ScheduledDateTime,
                item.Appointment.CompletedAtUtc,
                item.Appointment.Status,
                item.Appointment.PaymentStatus,
                item.Appointment.PaymentMethod,
                item.Appointment.TotalPrice,
                item.Appointment.AmountPaid,
                item.Appointment.ProfessionalName,
                item.Procedure.RecommendedReturnDays))
            .ToListAsync(cancellationToken);

        var history = await GetClientCompletionHistoryAsync(rows.Select(item => item.ClientId).Distinct().ToArray(), cancellationToken);
        var columns = fields.Select(field => new CustomReportColumnDto(field, FieldLabels[field])).ToArray();
        var resultRows = rows.Select(row => new CustomReportRowDto(
            row.AppointmentId,
            row.ClientId,
            BuildValues(fields, row, history))).ToArray();

        return new CustomReportResultDto(columns, resultRows, totalCount);
    }

    private IReadOnlyList<DashboardScheduleDayDto> BuildUpcomingSchedule(
        DateOnly today,
        IReadOnlyList<Appointment> appointments) =>
        Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var date = today.AddDays(offset);
                var dayAppointments = appointments.Where(appointment => ToLocalDate(appointment.ScheduledDateTime) == date).ToList();
                return new DashboardScheduleDayDto(
                    date,
                    CountStatus(dayAppointments, AppointmentStatus.Scheduled),
                    CountStatus(dayAppointments, AppointmentStatus.Confirmed),
                    CountStatus(dayAppointments, AppointmentStatus.InProgress),
                    CountStatus(dayAppointments, AppointmentStatus.Completed),
                    CountStatus(dayAppointments, AppointmentStatus.Cancelled),
                    CountStatus(dayAppointments, AppointmentStatus.NoShow));
            })
            .ToList();

    private static IReadOnlyList<DashboardBirthdayDto> BuildUpcomingBirthdays(
        DateOnly today,
        IReadOnlyList<ClientBirthday> clients) =>
        clients
            .Select(client =>
            {
                var nextBirthday = BirthdayInYear(client.BirthDate, today.Year);
                if (nextBirthday < today)
                {
                    nextBirthday = BirthdayInYear(client.BirthDate, today.Year + 1);
                }

                return new DashboardBirthdayDto(
                    client.Id,
                    client.Name,
                    client.BirthDate,
                    nextBirthday.Year - client.BirthDate.Year,
                    nextBirthday.DayNumber - today.DayNumber);
            })
            .Where(item => item.DaysUntil <= 30)
            .OrderBy(item => item.DaysUntil)
            .ThenBy(item => item.Name)
            .Take(6)
            .ToList();

    private IReadOnlyList<DashboardReturnAlertDto> BuildDueReturns(
        DateOnly today,
        IEnumerable<ReturnCandidate> candidates) =>
        candidates
            .GroupBy(item => new { item.ClientId, item.ProcedureTypeId })
            .Select(group => group.OrderByDescending(item => item.CompletedAtUtc).First())
            .Select(item =>
            {
                var completedDate = ToLocalDate(item.CompletedAtUtc);
                var returnDate = completedDate.AddDays(item.RecommendedReturnDays!.Value);
                return new DashboardReturnAlertDto(
                    item.ClientId,
                    item.ClientName,
                    item.ProcedureName,
                    completedDate,
                    returnDate,
                    returnDate.DayNumber - today.DayNumber);
            })
            .Where(item => item.DaysUntil <= 14)
            .OrderBy(item => item.DaysUntil)
            .ThenBy(item => item.ClientName)
            .ToArray();

    private async Task<IReadOnlyDictionary<Guid, ClientCompletionHistory>> GetClientCompletionHistoryAsync(
        IReadOnlyCollection<Guid> clientIds,
        CancellationToken cancellationToken)
    {
        if (clientIds.Count == 0) return new Dictionary<Guid, ClientCompletionHistory>();

        var rows = await (
                from appointment in db.Appointments.AsNoTracking()
                join procedure in db.ProcedureTypes.AsNoTracking() on appointment.ProcedureTypeId equals procedure.Id
                where clientIds.Contains(appointment.ClientId) && appointment.Status == AppointmentStatus.Completed
                select new ReturnCandidate(
                    appointment.ClientId,
                    appointment.ClientName,
                    appointment.ProcedureTypeId,
                    appointment.ProcedureName,
                    appointment.CompletedAtUtc ?? appointment.ScheduledDateTime,
                    procedure.RecommendedReturnDays))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.ClientId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var last = group.OrderByDescending(item => item.CompletedAtUtc).First();
                    var returns = group
                        .Where(item => item.RecommendedReturnDays.HasValue)
                        .GroupBy(item => item.ProcedureTypeId)
                        .ToDictionary(
                            procedure => procedure.Key,
                            procedure => procedure.OrderByDescending(item => item.CompletedAtUtc).First());
                    return new ClientCompletionHistory(last, returns);
                });
    }

    private IReadOnlyDictionary<string, string?> BuildValues(
        IReadOnlyCollection<string> fields,
        ReportSourceRow row,
        IReadOnlyDictionary<Guid, ClientCompletionHistory> history)
    {
        history.TryGetValue(row.ClientId, out var clientHistory);
        ReturnCandidate? latestProcedureReturn = null;
        if (clientHistory is not null)
        {
            clientHistory.LatestReturns.TryGetValue(row.ProcedureTypeId, out latestProcedureReturn);
        }
        var isLatestReturn = row.Status == AppointmentStatus.Completed &&
                             latestProcedureReturn is not null &&
                             latestProcedureReturn.CompletedAtUtc == (row.CompletedAtUtc ?? row.ScheduledDateTime);
        var returnDate = isLatestReturn && latestProcedureReturn!.RecommendedReturnDays.HasValue
            ? ToLocalDate(latestProcedureReturn.CompletedAtUtc).AddDays(latestProcedureReturn.RecommendedReturnDays.Value)
            : (DateOnly?)null;
        var today = clock.LocalToday;

        return fields.ToDictionary(field => field, field => field switch
        {
            CustomReportFields.ClientName => row.ClientName,
            CustomReportFields.ClientPhone => row.ClientPhone,
            CustomReportFields.ClientCity => row.ClientCity,
            CustomReportFields.ClientBirthDate => FormatDate(row.ClientBirthDate),
            CustomReportFields.ClientSource => row.ClientSource,
            CustomReportFields.ProcedureName => row.ProcedureName,
            CustomReportFields.ScheduledDate => FormatDateTime(row.ScheduledDateTime),
            CustomReportFields.CompletedDate => row.CompletedAtUtc.HasValue ? FormatDateTime(row.CompletedAtUtc.Value) : null,
            CustomReportFields.AppointmentStatus => AppointmentStatusLabel(row.Status),
            CustomReportFields.PaymentStatus => PaymentStatusLabel(row.PaymentStatus),
            CustomReportFields.PaymentMethod => PaymentMethodLabel(row.PaymentMethod),
            CustomReportFields.TotalPrice => FormatCurrency(row.TotalPrice),
            CustomReportFields.AmountPaid => FormatCurrency(row.AmountPaid),
            CustomReportFields.OutstandingAmount => FormatCurrency(Math.Max(0, row.TotalPrice - row.AmountPaid)),
            CustomReportFields.ProfessionalName => row.ProfessionalName,
            CustomReportFields.LastCompletedProcedure => clientHistory?.LastCompleted.ProcedureName,
            CustomReportFields.LastCompletedDate => clientHistory is null ? null : FormatDateTime(clientHistory.LastCompleted.CompletedAtUtc),
            CustomReportFields.ReturnDueDate => FormatDate(returnDate),
            CustomReportFields.ReturnStatus => returnDate is null ? null : ReturnStatusLabel(returnDate.Value, today),
            _ => null
        }, StringComparer.Ordinal);
    }

    private static IQueryable<ReportQueryRow> ApplySort(
        IQueryable<ReportQueryRow> query,
        string? sortBy,
        bool descending) => sortBy switch
    {
        CustomReportFields.ClientName => descending
            ? query.OrderByDescending(item => item.Client.Name).ThenByDescending(item => item.Appointment.ScheduledDateTime)
            : query.OrderBy(item => item.Client.Name).ThenByDescending(item => item.Appointment.ScheduledDateTime),
        CustomReportFields.ProcedureName => descending
            ? query.OrderByDescending(item => item.Appointment.ProcedureName).ThenByDescending(item => item.Appointment.ScheduledDateTime)
            : query.OrderBy(item => item.Appointment.ProcedureName).ThenByDescending(item => item.Appointment.ScheduledDateTime),
        CustomReportFields.CompletedDate => descending
            ? query.OrderByDescending(item => item.Appointment.CompletedAtUtc ?? item.Appointment.ScheduledDateTime)
            : query.OrderBy(item => item.Appointment.CompletedAtUtc ?? item.Appointment.ScheduledDateTime),
        _ => descending
            ? query.OrderByDescending(item => item.Appointment.ScheduledDateTime)
            : query.OrderBy(item => item.Appointment.ScheduledDateTime)
    };

    private static void ValidateCustomReportRequest(CustomReportRequest request)
    {
        if (request.FromDate.HasValue && request.ToDate.HasValue && request.FromDate > request.ToDate)
        {
            throw new BusinessRuleException("A data inicial não pode ser posterior à data final.");
        }

        if (request.BirthdayMonth is < 1 or > 12)
        {
            throw new BusinessRuleException("O mês de aniversário deve ficar entre 1 e 12.");
        }

        var invalidField = request.Fields.FirstOrDefault(field => !CustomReportFields.All.Contains(field));
        if (invalidField is not null)
        {
            throw new BusinessRuleException("Um dos campos selecionados não está disponível para este relatório.");
        }
    }

    private static readonly IReadOnlyDictionary<string, string> FieldLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [CustomReportFields.ClientName] = "Cliente",
        [CustomReportFields.ClientPhone] = "Telefone",
        [CustomReportFields.ClientCity] = "Cidade",
        [CustomReportFields.ClientBirthDate] = "Nascimento",
        [CustomReportFields.ClientSource] = "Origem",
        [CustomReportFields.ProcedureName] = "Procedimento",
        [CustomReportFields.ScheduledDate] = "Data agendada",
        [CustomReportFields.CompletedDate] = "Data concluída",
        [CustomReportFields.AppointmentStatus] = "Status do atendimento",
        [CustomReportFields.PaymentStatus] = "Status do pagamento",
        [CustomReportFields.PaymentMethod] = "Forma de pagamento",
        [CustomReportFields.TotalPrice] = "Valor total",
        [CustomReportFields.AmountPaid] = "Valor pago",
        [CustomReportFields.OutstandingAmount] = "Saldo",
        [CustomReportFields.ProfessionalName] = "Profissional",
        [CustomReportFields.LastCompletedProcedure] = "Último procedimento",
        [CustomReportFields.LastCompletedDate] = "Última conclusão",
        [CustomReportFields.ReturnDueDate] = "Retorno recomendado",
        [CustomReportFields.ReturnStatus] = "Situação do retorno"
    };

    private string FormatDateTime(DateTime value) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(value, DateTimeKind.Utc), clock.TimeZone).ToString("dd/MM/yyyy HH:mm");

    private static string? FormatDate(DateOnly? value) => value?.ToString("dd/MM/yyyy");

    private static string FormatCurrency(decimal value) => value.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));

    private static string AppointmentStatusLabel(AppointmentStatus value) => value switch
    {
        AppointmentStatus.Scheduled => "Marcado",
        AppointmentStatus.Confirmed => "Confirmado",
        AppointmentStatus.InProgress => "Em atendimento",
        AppointmentStatus.Completed => "Concluído",
        AppointmentStatus.Cancelled => "Cancelado",
        AppointmentStatus.NoShow => "Não compareceu",
        _ => value.ToString()
    };

    private static string PaymentStatusLabel(PaymentStatus value) => value switch
    {
        PaymentStatus.Paid => "Pago",
        PaymentStatus.Partial => "Parcial",
        _ => "Pendente"
    };

    private static string PaymentMethodLabel(PaymentMethod value) => value switch
    {
        PaymentMethod.CreditCard => "Cartão de crédito",
        PaymentMethod.DebitCard => "Cartão de débito",
        PaymentMethod.Cash => "Dinheiro",
        PaymentMethod.PackageSession => "Pacote de sessões",
        _ => "PIX"
    };

    private static string ReturnStatusLabel(DateOnly returnDate, DateOnly today) => returnDate < today
        ? $"Vencido há {today.DayNumber - returnDate.DayNumber} dia(s)"
        : returnDate == today
            ? "Vence hoje"
            : $"Faltam {returnDate.DayNumber - today.DayNumber} dia(s)";

    private static DateOnly BirthdayInYear(DateOnly birthDate, int year) =>
        new(year, birthDate.Month, Math.Min(birthDate.Day, DateTime.DaysInMonth(year, birthDate.Month)));

    private DateOnly ToLocalDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            clock.TimeZone));

    private static int CountStatus(IEnumerable<Appointment> appointments, AppointmentStatus status) =>
        appointments.Count(appointment => appointment.Status == status);

    private sealed class ReportQueryRow
    {
        public Appointment Appointment { get; init; } = null!;
        public Client Client { get; init; } = null!;
        public ProcedureType Procedure { get; init; } = null!;
    }

    private sealed record ReportSourceRow(
        Guid AppointmentId,
        Guid ClientId,
        string ClientName,
        string ClientPhone,
        string? ClientCity,
        DateOnly? ClientBirthDate,
        string ClientSource,
        Guid ProcedureTypeId,
        string ProcedureName,
        DateTime ScheduledDateTime,
        DateTime? CompletedAtUtc,
        AppointmentStatus Status,
        PaymentStatus PaymentStatus,
        PaymentMethod PaymentMethod,
        decimal TotalPrice,
        decimal AmountPaid,
        string ProfessionalName,
        int? RecommendedReturnDays);

    private sealed record ReturnCandidate(
        Guid ClientId,
        string ClientName,
        Guid ProcedureTypeId,
        string ProcedureName,
        DateTime CompletedAtUtc,
        int? RecommendedReturnDays);

    private sealed record ClientCompletionHistory(
        ReturnCandidate LastCompleted,
        IReadOnlyDictionary<Guid, ReturnCandidate> LatestReturns);

    private sealed record ClientBirthday(Guid Id, string Name, DateOnly BirthDate);

    private DateTime ToUtc(DateOnly localDate)
    {
        var local = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, clock.TimeZone);
    }
}
