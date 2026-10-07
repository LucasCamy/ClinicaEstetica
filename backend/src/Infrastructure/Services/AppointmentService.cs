using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Appointments;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Settings;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class AppointmentService(
    EsteticaDbContext db,
    IClinicClock clock,
    IClinicSettingsService clinicSettings) : IAppointmentService
{
    public async Task<PagedResult<AppointmentDto>> ListAsync(
        PageRequest page,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var query = db.Appointments.AsNoTracking();
        if (fromUtc.HasValue) query = query.Where(item => item.ScheduledDateTime >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(item => item.ScheduledDateTime < toUtc.Value);

        var search = page.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLower();
            query = query.Where(item =>
                item.ClientName.ToLower().Contains(normalized) ||
                item.ProcedureName.ToLower().Contains(normalized));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderBy(item => item.ScheduledDateTime)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AppointmentDto>(
            entities.Select(item => item.ToDto()).ToArray(),
            page.SafePage,
            page.SafePageSize,
            total);
    }

    public Task<AppointmentDto> CreateAsync(SaveAppointmentRequest request, CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<AppointmentDto> UpdateAsync(Guid id, SaveAppointmentRequest request, CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public async Task<AppointmentDto> UpdateStatusAsync(
        Guid id,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                          ?? throw new ResourceNotFoundException("Agendamento", id);

        var amountPaid = request.AmountPaid ?? appointment.AmountPaid;
        EnsurePaymentIsValid(appointment.TotalPrice, amountPaid);
        var wasCompleted = appointment.Status == AppointmentStatus.Completed;
        appointment.Status = request.Status;
        appointment.CompletedAtUtc = request.Status == AppointmentStatus.Completed
            ? wasCompleted ? appointment.CompletedAtUtc ?? clock.UtcNow : clock.UtcNow
            : null;
        appointment.AmountPaid = amountPaid;
        if (request.PaymentMethod.HasValue) appointment.PaymentMethod = request.PaymentMethod.Value;
        appointment.PaymentStatus = CalculatePaymentStatus(appointment.TotalPrice, amountPaid);
        await db.SaveChangesAsync(cancellationToken);
        return appointment.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                          ?? throw new ResourceNotFoundException("Agendamento", id);
        if (appointment.Status == AppointmentStatus.Completed)
        {
            throw new BusinessRuleException("Atendimentos concluídos não podem ser removidos; registre uma evolução ou ajuste o cadastro quando necessário.");
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppointmentDto> SaveAsync(
        Guid? id,
        SaveAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ScheduledDateTime == default)
        {
            throw new BusinessRuleException("A data e hora do agendamento são obrigatórias.");
        }

        EnsurePaymentIsValid(request.TotalPrice, request.AmountPaid);
        var client = await db.Clients.SingleOrDefaultAsync(item => item.Id == request.ClientId, cancellationToken)
                     ?? throw new ResourceNotFoundException("Cliente", request.ClientId);
        var appointment = id.HasValue
            ? await db.Appointments.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
              ?? throw new ResourceNotFoundException("Agendamento", id.Value)
            : new Appointment { Id = Guid.NewGuid(), CreatedAt = clock.UtcNow };

        var procedure = await db.ProcedureTypes.SingleOrDefaultAsync(
                            item => item.Id == request.ProcedureTypeId,
                            cancellationToken)
                        ?? throw new ResourceNotFoundException("Procedimento", request.ProcedureTypeId);
        if (!procedure.IsActive && (!id.HasValue || appointment.ProcedureTypeId != procedure.Id))
        {
            throw new BusinessRuleException("Procedimentos inativos não podem ser usados em novos agendamentos.");
        }

        var normalizedScheduledAt = NormalizeToUtc(request.ScheduledDateTime);
        // Um atendimento já existente fora do expediente pode ser corrigido sem alterar o horário.
        // Somente novas marcações ou mudanças de data/hora precisam obedecer à nova configuração.
        var isNewSchedule = !id.HasValue || appointment.ScheduledDateTime != normalizedScheduledAt;
        if (isNewSchedule && !await clinicSettings.IsWithinOperatingHoursAsync(normalizedScheduledAt, cancellationToken))
        {
            throw new BusinessRuleException("O horário escolhido está fora do expediente configurado. Ajuste a agenda em Configurações ou escolha outro horário.");
        }

        appointment.ClientId = client.Id;
        appointment.ClientName = client.Name;
        appointment.ProcedureTypeId = procedure.Id;
        appointment.ProcedureName = procedure.Name;
        appointment.ScheduledDateTime = normalizedScheduledAt;
        var wasCompleted = appointment.Status == AppointmentStatus.Completed;
        appointment.Status = request.Status;
        appointment.CompletedAtUtc = request.Status == AppointmentStatus.Completed
            ? wasCompleted ? appointment.CompletedAtUtc ?? clock.UtcNow : clock.UtcNow
            : null;
        appointment.TotalPrice = request.TotalPrice;
        appointment.AmountPaid = request.AmountPaid;
        appointment.PaymentMethod = request.PaymentMethod;
        appointment.PaymentStatus = CalculatePaymentStatus(request.TotalPrice, request.AmountPaid);
        appointment.ProfessionalName = request.ProfessionalName.Trim();
        appointment.Notes = request.Notes?.Trim();

        if (!id.HasValue) db.Appointments.Add(appointment);
        await db.SaveChangesAsync(cancellationToken);
        return appointment.ToDto();
    }

    private static void EnsurePaymentIsValid(decimal totalPrice, decimal amountPaid)
    {
        if (totalPrice < 0 || amountPaid < 0)
        {
            throw new BusinessRuleException("Valores financeiros não podem ser negativos.");
        }

        if (amountPaid > totalPrice)
        {
            throw new BusinessRuleException("O valor pago não pode exceder o valor total.");
        }
    }

    private static PaymentStatus CalculatePaymentStatus(decimal totalPrice, decimal amountPaid) =>
        amountPaid <= 0
            ? PaymentStatus.Pending
            : amountPaid >= totalPrice
                ? PaymentStatus.Paid
                : PaymentStatus.Partial;

    private DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), clock.TimeZone)
    };
}
