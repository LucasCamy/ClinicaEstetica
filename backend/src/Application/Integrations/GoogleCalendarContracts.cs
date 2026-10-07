namespace PainelEstetica.Application.Integrations;

public sealed record GoogleCalendarConnectionDto(
    bool IsConfigured,
    bool IsConnected,
    bool HasSelectedCalendar,
    string? CalendarId,
    string? CalendarName,
    DateTime? ConnectedAtUtc,
    DateTime? LastSuccessfulSyncAtUtc,
    string? SetupMessage);

public sealed record GoogleCalendarAuthorizationDto(string AuthorizationUrl);

public sealed record GoogleCalendarListItemDto(
    string Id,
    string Name,
    bool IsPrimary,
    string AccessRole,
    string? Color);

public sealed record GoogleCalendarEventDto(
    string Id,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsAllDay,
    string? HtmlLink,
    string? Color);

public sealed record GoogleCalendarEventsDto(
    bool IsConnected,
    bool HasSelectedCalendar,
    IReadOnlyList<GoogleCalendarEventDto> Events,
    string? Notice);

public sealed class SelectGoogleCalendarRequest
{
    public string CalendarId { get; init; } = string.Empty;
}

public sealed record GoogleCalendarAuthorizationState(
    Guid ActorUserId,
    string ActorUsername,
    string ProtectedState);

public sealed record GoogleCalendarSyncResult(bool Attempted, string? Warning);

public interface IGoogleCalendarService
{
    Task<GoogleCalendarConnectionDto> GetConnectionAsync(CancellationToken cancellationToken);
    Task<GoogleCalendarAuthorizationState> StartAuthorizationAsync(
        Guid actorUserId,
        string actorUsername,
        CancellationToken cancellationToken);
    string GetAuthorizationUrl(string protectedState);
    Task<GoogleCalendarAuthorizationState> CompleteAuthorizationAsync(string authorizationCode, string protectedState, CancellationToken cancellationToken);
    Task<IReadOnlyList<GoogleCalendarListItemDto>> ListCalendarsAsync(CancellationToken cancellationToken);
    Task<GoogleCalendarConnectionDto> SelectCalendarAsync(string calendarId, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<GoogleCalendarEventsDto> ListExternalEventsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<GoogleCalendarSyncResult> SyncAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken);
}
