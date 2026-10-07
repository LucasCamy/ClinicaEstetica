using PainelEstetica.Application.Appointments;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Integrations;
using PainelEstetica.Application.Security;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Appointments;

public static class AppointmentEndpoints
{
    public static void MapAppointmentEndpoints(this RouteGroupBuilder securedApi)
    {
        var appointments = securedApi.MapGroup("/appointments").WithTags("Appointments");

        appointments.MapGet("/", async (
            int? page,
            int? pageSize,
            string? search,
            DateTime? fromUtc,
            DateTime? toUtc,
            IAppointmentService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                new PageRequest(page ?? 1, pageSize ?? 25, search),
                fromUtc,
                toUtc,
                cancellationToken)))
            .RequireAuthorization(AppPermissions.AppointmentRead);

        appointments.MapPost("/", async (
            SaveAppointmentRequest request,
            HttpContext context,
            IAppointmentService service,
            IGoogleCalendarService googleCalendar,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var appointment = await service.CreateAsync(request, cancellationToken);
            var sync = await googleCalendar.SyncAppointmentAsync(appointment.Id, cancellationToken);
            appointment = appointment with { GoogleCalendarSyncWarning = sync.Warning };
            await audit.WriteAsync(context.ToAuditContext(), "Appointment.Created", "Appointment", appointment.Id.ToString(), cancellationToken: cancellationToken);
            return Results.Created($"/api/appointments/{appointment.Id}", appointment);
        })
            .AddEndpointFilter<ValidationFilter<SaveAppointmentRequest>>()
            .RequireAuthorization(AppPermissions.AppointmentManage)
            .Mutating();

        appointments.MapPut("/{id:guid}", async (
            Guid id,
            SaveAppointmentRequest request,
            HttpContext context,
            IAppointmentService service,
            IGoogleCalendarService googleCalendar,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var appointment = await service.UpdateAsync(id, request, cancellationToken);
            var sync = await googleCalendar.SyncAppointmentAsync(id, cancellationToken);
            appointment = appointment with { GoogleCalendarSyncWarning = sync.Warning };
            await audit.WriteAsync(context.ToAuditContext(), "Appointment.Updated", "Appointment", id.ToString(), cancellationToken: cancellationToken);
            return Results.Ok(appointment);
        })
            .AddEndpointFilter<ValidationFilter<SaveAppointmentRequest>>()
            .RequireAuthorization(AppPermissions.AppointmentManage)
            .Mutating();

        appointments.MapPut("/{id:guid}/status", async (
            Guid id,
            UpdateAppointmentStatusRequest request,
            HttpContext context,
            IAppointmentService service,
            IGoogleCalendarService googleCalendar,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var appointment = await service.UpdateStatusAsync(id, request, cancellationToken);
            var sync = await googleCalendar.SyncAppointmentAsync(id, cancellationToken);
            appointment = appointment with { GoogleCalendarSyncWarning = sync.Warning };
            await audit.WriteAsync(
                context.ToAuditContext(),
                "Appointment.StatusChanged",
                "Appointment",
                id.ToString(),
                details: new { appointment.Status, appointment.PaymentStatus },
                cancellationToken: cancellationToken);
            return Results.Ok(appointment);
        })
            .AddEndpointFilter<ValidationFilter<UpdateAppointmentStatusRequest>>()
            .RequireAuthorization(AppPermissions.AppointmentManage)
            .Mutating();

        appointments.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IAppointmentService service,
            IGoogleCalendarService googleCalendar,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id, cancellationToken);
            await googleCalendar.SyncAppointmentAsync(id, cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "Appointment.Cancelled", "Appointment", id.ToString(), cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .RequireAuthorization(AppPermissions.AppointmentManage)
            .Mutating();
    }
}
