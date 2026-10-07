using Microsoft.AspNetCore.Http;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Integrations;
using PainelEstetica.Application.Security;
using PainelEstetica.WebAPI.Endpoints;

namespace PainelEstetica.WebAPI.Modules.Integrations;

public static class GoogleCalendarEndpoints
{
    private const string StateCookieName = "Clinica.GoogleCalendar.OAuthState";

    public static void MapGoogleCalendarCallbackEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/integrations/google-calendar/callback", async (
            string? code,
            string? state,
            string? error,
            HttpContext context,
            IGoogleCalendarService service,
            IAuditService audit,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var expectedState = context.Request.Cookies[StateCookieName];
            context.Response.Cookies.Delete(StateCookieName, new CookieOptions { Path = "/api/integrations/google-calendar" });
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Redirect("/admin/configuracoes?google=denied");
            }
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(expectedState) || !string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                return Results.Redirect("/admin/configuracoes?google=invalid");
            }

            try
            {
                var authorization = await service.CompleteAuthorizationAsync(code, state, cancellationToken);
                await audit.WriteAsync(
                    new AuditContext(
                        authorization.ActorUserId,
                        authorization.ActorUsername,
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.Request.Headers.UserAgent.ToString()),
                    "GoogleCalendar.Connected",
                    "GoogleCalendarConnection",
                    cancellationToken: cancellationToken);
                return Results.Redirect("/admin/configuracoes?google=connected");
            }
            catch (BusinessRuleException exception)
            {
                // O navegador recebe uma mensagem segura; a causa técnica fica somente no log do servidor.
                loggerFactory.CreateLogger("GoogleCalendarOAuthCallback")
                    .LogWarning(exception, "Falha ao concluir o callback OAuth do Google Calendar.");
                return Results.Redirect("/admin/configuracoes?google=authorization_failed");
            }
            catch (Exception exception)
            {
                loggerFactory.CreateLogger("GoogleCalendarOAuthCallback")
                    .LogError(exception, "Falha inesperada ao concluir o callback OAuth do Google Calendar.");
                return Results.Redirect("/admin/configuracoes?google=failed");
            }
        });
    }

    public static void MapGoogleCalendarEndpoints(this RouteGroupBuilder securedApi)
    {
        var google = securedApi.MapGroup("/integrations/google-calendar").WithTags("Google Calendar");

        google.MapGet("/connection", async (IGoogleCalendarService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetConnectionAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.SettingsManage);

        google.MapPost("/connect", async (
            HttpContext context,
            IGoogleCalendarService service,
            CancellationToken cancellationToken) =>
        {
            var auditContext = context.ToAuditContext();
            if (!auditContext.ActorUserId.HasValue)
            {
                return Results.Unauthorized();
            }
            var authorization = await service.StartAuthorizationAsync(auditContext.ActorUserId.Value, auditContext.ActorUsername, cancellationToken);
            context.Response.Cookies.Append(StateCookieName, authorization.ProtectedState, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/integrations/google-calendar",
                MaxAge = TimeSpan.FromMinutes(10),
                IsEssential = true
            });
            return Results.Ok(new GoogleCalendarAuthorizationDto(service.GetAuthorizationUrl(authorization.ProtectedState)));
        })
            .RequireAuthorization(AppPermissions.SettingsManage)
            .Mutating();

        google.MapGet("/calendars", async (IGoogleCalendarService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListCalendarsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.SettingsManage);

        google.MapPut("/calendar", async (
            SelectGoogleCalendarRequest request,
            HttpContext context,
            IGoogleCalendarService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            var connection = await service.SelectCalendarAsync(request.CalendarId, cancellationToken);
            await audit.WriteAsync(
                context.ToAuditContext(),
                "GoogleCalendar.CalendarSelected",
                "GoogleCalendarConnection",
                details: new { connection.CalendarId, connection.CalendarName },
                cancellationToken: cancellationToken);
            return Results.Ok(connection);
        })
            .AddEndpointFilter<ValidationFilter<SelectGoogleCalendarRequest>>()
            .RequireAuthorization(AppPermissions.SettingsManage)
            .Mutating();

        google.MapDelete("/connection", async (
            HttpContext context,
            IGoogleCalendarService service,
            IAuditService audit,
            CancellationToken cancellationToken) =>
        {
            await service.DisconnectAsync(cancellationToken);
            await audit.WriteAsync(context.ToAuditContext(), "GoogleCalendar.Disconnected", "GoogleCalendarConnection", cancellationToken: cancellationToken);
            return Results.NoContent();
        })
            .RequireAuthorization(AppPermissions.SettingsManage)
            .Mutating();

        google.MapGet("/events", async (
            DateTime fromUtc,
            DateTime toUtc,
            IGoogleCalendarService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListExternalEventsAsync(fromUtc, toUtc, cancellationToken)))
            .RequireAuthorization(AppPermissions.AppointmentRead);
    }
}
