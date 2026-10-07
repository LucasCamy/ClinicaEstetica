using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Integrations;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        Uri.TryCreate(RedirectUri, UriKind.Absolute, out _);
}

public sealed class GoogleCalendarService(
    EsteticaDbContext db,
    IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider,
    IClock clock,
    IClinicClock clinicClock,
    IOptions<GoogleCalendarOptions> optionsAccessor,
    ILogger<GoogleCalendarService> logger) : IGoogleCalendarService
{
    private const string ConnectionPurpose = "PainelEstetica.GoogleCalendar.RefreshToken.v1";
    private const string StatePurpose = "PainelEstetica.GoogleCalendar.OAuthState.v1";
    private const string IntegrationMarker = "painel-estetica";
    private const string GoogleCalendarApiBase = "https://www.googleapis.com/calendar/v3";
    private const string GoogleOAuthAuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string GoogleOAuthTokenEndpoint = "https://oauth2.googleapis.com/token";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly GoogleCalendarOptions options = optionsAccessor.Value;
    private readonly IDataProtector refreshTokenProtector = dataProtectionProvider.CreateProtector(ConnectionPurpose);
    private readonly IDataProtector stateProtector = dataProtectionProvider.CreateProtector(StatePurpose);

    public async Task<GoogleCalendarConnectionDto> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await db.GoogleCalendarConnections.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return ToConnectionDto(connection);
    }

    public Task<GoogleCalendarAuthorizationState> StartAuthorizationAsync(
        Guid actorUserId,
        string actorUsername,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var issuedAt = clock.UtcNow;
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
        var state = string.Join('|', actorUserId.ToString("N"), actorUsername[..Math.Min(actorUsername.Length, 80)], issuedAt.Ticks, nonce);
        return Task.FromResult(new GoogleCalendarAuthorizationState(
            actorUserId,
            actorUsername,
            stateProtector.Protect(state)));
    }

    public string GetAuthorizationUrl(string protectedState)
    {
        EnsureConfigured();
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.calendarlist.readonly",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true",
            ["state"] = protectedState
        };
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(GoogleOAuthAuthorizationEndpoint, query);
    }

    public async Task<GoogleCalendarAuthorizationState> CompleteAuthorizationAsync(string authorizationCode, string protectedState, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new BusinessRuleException("O Google não retornou um código de autorização. Tente conectar novamente.");
        }

        var state = ReadState(protectedState);
        var token = await ExchangeAuthorizationCodeAsync(authorizationCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new BusinessRuleException("O Google não forneceu uma autorização persistente. Remova o acesso do Painel Estética na sua Conta Google e conecte novamente.");
        }

        var connection = await db.GoogleCalendarConnections.SingleOrDefaultAsync(cancellationToken);
        if (connection is null)
        {
            connection = new GoogleCalendarConnection { Id = Guid.NewGuid() };
            db.GoogleCalendarConnections.Add(connection);
        }

        connection.EncryptedRefreshToken = refreshTokenProtector.Protect(token.RefreshToken);
        connection.CalendarId = null;
        connection.CalendarName = null;
        connection.ConnectedAtUtc = clock.UtcNow;
        connection.LastSuccessfulSyncAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    public async Task<IReadOnlyList<GoogleCalendarListItemDto>> ListCalendarsAsync(CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(cancellationToken);
        using var request = AuthorizedRequest(HttpMethod.Get, $"{GoogleCalendarApiBase}/users/me/calendarList?minAccessRole=writer", accessToken);
        using var response = await SendAsync(request, cancellationToken);
        var payload = await ReadPayloadAsync<CalendarListResponse>(response, cancellationToken);
        return payload.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && IsWritable(item.AccessRole))
            .OrderByDescending(item => item.Primary)
            .ThenBy(item => item.Summary, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new GoogleCalendarListItemDto(
                item.Id!,
                string.IsNullOrWhiteSpace(item.Summary) ? "Agenda sem nome" : item.Summary,
                item.Primary,
                item.AccessRole ?? "reader",
                item.BackgroundColor))
            .ToArray();
    }

    public async Task<GoogleCalendarConnectionDto> SelectCalendarAsync(string calendarId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(calendarId))
        {
            throw new BusinessRuleException("Selecione uma agenda do Google para concluir a integração.");
        }

        var calendars = await ListCalendarsAsync(cancellationToken);
        var selected = calendars.SingleOrDefault(item => string.Equals(item.Id, calendarId, StringComparison.Ordinal));
        if (selected is null)
        {
            throw new BusinessRuleException("A agenda selecionada não está disponível para edição nesta conta Google.");
        }

        var connection = await RequireConnectionAsync(cancellationToken);
        connection.CalendarId = selected.Id;
        connection.CalendarName = selected.Name;
        await db.SaveChangesAsync(cancellationToken);
        return ToConnectionDto(connection);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var connection = await db.GoogleCalendarConnections.SingleOrDefaultAsync(cancellationToken);
        if (connection is null) return;
        db.GoogleCalendarConnections.Remove(connection);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GoogleCalendarEventsDto> ListExternalEventsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (toUtc <= fromUtc || toUtc - fromUtc > TimeSpan.FromDays(93))
        {
            throw new BusinessRuleException("Escolha um intervalo de agenda válido de até 93 dias.");
        }

        var connection = await db.GoogleCalendarConnections.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (!options.IsConfigured || connection is null || string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
        {
            return new GoogleCalendarEventsDto(false, false, [], "Conecte uma conta Google para visualizar a agenda compartilhada.");
        }
        if (string.IsNullOrWhiteSpace(connection.CalendarId))
        {
            return new GoogleCalendarEventsDto(true, false, [], "Escolha a agenda compartilhada nas integrações.");
        }

        try
        {
            var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
            var url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
                $"{GoogleCalendarApiBase}/calendars/{Uri.EscapeDataString(connection.CalendarId)}/events",
                new Dictionary<string, string?>
                {
                    ["timeMin"] = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).ToString("O"),
                    ["timeMax"] = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc).ToString("O"),
                    ["singleEvents"] = "true",
                    ["orderBy"] = "startTime",
                    ["showDeleted"] = "false"
                });
            using var request = AuthorizedRequest(HttpMethod.Get, url, accessToken);
            using var response = await SendAsync(request, cancellationToken);
            var payload = await ReadPayloadAsync<EventListResponse>(response, cancellationToken);
            var events = payload.Items
                .Where(item => item.Status != "cancelled" && !IsInternalAppointment(item))
                .Select(ToEventDto)
                .Where(item => item is not null)
                .Cast<GoogleCalendarEventDto>()
                .ToArray();

            await MarkSuccessfulSyncAsync(connection.Id, cancellationToken);
            return new GoogleCalendarEventsDto(true, true, events, null);
        }
        catch (BusinessRuleException exception)
        {
            logger.LogWarning(exception, "Falha ao consultar eventos do Google Calendar.");
            return new GoogleCalendarEventsDto(true, true, [], exception.Message);
        }
    }

    public async Task<GoogleCalendarSyncResult> SyncAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        var connection = await db.GoogleCalendarConnections.SingleOrDefaultAsync(cancellationToken);
        if (!options.IsConfigured || connection is null || string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken) || string.IsNullOrWhiteSpace(connection.CalendarId))
        {
            return new GoogleCalendarSyncResult(false, null);
        }

        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
        if (appointment is null) return new GoogleCalendarSyncResult(false, null);
        var procedureDuration = await db.ProcedureTypes
            .Where(item => item.Id == appointment.ProcedureTypeId)
            .Select(item => item.DurationMinutes)
            .SingleOrDefaultAsync(cancellationToken);
        var duration = Math.Clamp(procedureDuration <= 0 ? 60 : procedureDuration, 5, 720);

        try
        {
            var accessToken = await GetAccessTokenAsync(connection, cancellationToken);
            if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
            {
                if (!string.IsNullOrWhiteSpace(appointment.GoogleCalendarEventId))
                {
                    using var deleteRequest = AuthorizedRequest(
                        HttpMethod.Delete,
                        $"{GoogleCalendarApiBase}/calendars/{Uri.EscapeDataString(connection.CalendarId)}/events/{Uri.EscapeDataString(appointment.GoogleCalendarEventId)}",
                        accessToken);
                    using var deleteResponse = await SendAsync(deleteRequest, cancellationToken, allowNotFound: true);
                    appointment.GoogleCalendarEventId = null;
                    await db.SaveChangesAsync(cancellationToken);
                }
                await MarkSuccessfulSyncAsync(connection.Id, cancellationToken);
                return new GoogleCalendarSyncResult(true, null);
            }

            var eventBody = BuildAppointmentEvent(appointment, duration);
            GoogleEventResource synced;
            if (string.IsNullOrWhiteSpace(appointment.GoogleCalendarEventId))
            {
                using var createRequest = AuthorizedRequest(
                    HttpMethod.Post,
                    $"{GoogleCalendarApiBase}/calendars/{Uri.EscapeDataString(connection.CalendarId)}/events",
                    accessToken,
                    eventBody);
                using var createResponse = await SendAsync(createRequest, cancellationToken);
                synced = await ReadPayloadAsync<GoogleEventResource>(createResponse, cancellationToken);
                appointment.GoogleCalendarEventId = synced.Id;
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                using var updateRequest = AuthorizedRequest(
                    HttpMethod.Put,
                    $"{GoogleCalendarApiBase}/calendars/{Uri.EscapeDataString(connection.CalendarId)}/events/{Uri.EscapeDataString(appointment.GoogleCalendarEventId)}",
                    accessToken,
                    eventBody);
                using var updateResponse = await SendAsync(updateRequest, cancellationToken, allowNotFound: true);
                if (updateResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    appointment.GoogleCalendarEventId = null;
                    await db.SaveChangesAsync(cancellationToken);
                    return await SyncAppointmentAsync(appointmentId, cancellationToken);
                }
            }

            await MarkSuccessfulSyncAsync(connection.Id, cancellationToken);
            return new GoogleCalendarSyncResult(true, null);
        }
        catch (BusinessRuleException exception)
        {
            logger.LogWarning(exception, "Atendimento {AppointmentId} foi salvo, mas não sincronizou com o Google Calendar.", appointmentId);
            return new GoogleCalendarSyncResult(true, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Atendimento {AppointmentId} foi salvo, mas ocorreu uma falha de rede no Google Calendar.", appointmentId);
            return new GoogleCalendarSyncResult(true, "O atendimento foi salvo, mas a sincronização com o Google não respondeu. Atualize a agenda mais tarde.");
        }
    }

    private GoogleCalendarConnectionDto ToConnectionDto(GoogleCalendarConnection? connection) => new(
        options.IsConfigured,
        connection is not null && !string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken),
        connection is not null && !string.IsNullOrWhiteSpace(connection.CalendarId),
        connection?.CalendarId,
        connection?.CalendarName,
        connection?.ConnectedAtUtc,
        connection?.LastSuccessfulSyncAtUtc,
        options.IsConfigured
            ? connection is null
                ? "Conecte a conta Google da profissional para escolher a agenda compartilhada."
                : string.IsNullOrWhiteSpace(connection.CalendarId)
                    ? "Escolha a agenda compartilhada que a conta conectada pode editar."
                    : null
            : "As credenciais do Google Calendar ainda não foram configuradas no servidor.");

    private void EnsureConfigured()
    {
        if (!options.IsConfigured)
        {
            throw new BusinessRuleException("Configure GOOGLE_CALENDAR_CLIENT_ID, GOOGLE_CALENDAR_CLIENT_SECRET e GOOGLE_CALENDAR_REDIRECT_URI no servidor antes de conectar o Google Calendar.");
        }
    }

    private GoogleCalendarAuthorizationState ReadState(string protectedState)
    {
        try
        {
            var values = stateProtector.Unprotect(protectedState).Split('|', 4, StringSplitOptions.None);
            if (values.Length != 4 || !Guid.TryParseExact(values[0], "N", out var actorId) || !long.TryParse(values[2], out var issuedTicks))
            {
                throw new CryptographicException("Estado inválido.");
            }

            var issuedAt = new DateTime(issuedTicks, DateTimeKind.Utc);
            if (issuedAt < clock.UtcNow.AddMinutes(-10) || issuedAt > clock.UtcNow.AddMinutes(1))
            {
                throw new CryptographicException("Estado expirado.");
            }
            return new GoogleCalendarAuthorizationState(actorId, values[1], protectedState);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            throw new BusinessRuleException("A autorização do Google expirou ou é inválida. Inicie a conexão novamente.");
        }
    }

    private async Task<GoogleCalendarConnection> RequireConnectionAsync(CancellationToken cancellationToken) =>
        await db.GoogleCalendarConnections.SingleOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException("Conecte uma conta Google antes de escolher a agenda compartilhada.");

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var connection = await RequireConnectionAsync(cancellationToken);
        return await GetAccessTokenAsync(connection, cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(GoogleCalendarConnection connection, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
        {
            throw new BusinessRuleException("A conexão com o Google não possui uma autorização válida. Conecte a conta novamente.");
        }

        string refreshToken;
        try
        {
            refreshToken = refreshTokenProtector.Unprotect(connection.EncryptedRefreshToken);
        }
        catch (CryptographicException)
        {
            throw new BusinessRuleException("A autorização protegida do Google não pode ser lida. Conecte a conta novamente.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Post, GoogleOAuthTokenEndpoint) { Content = content }, cancellationToken);
        var payload = await ReadPayloadAsync<GoogleTokenResponse>(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new BusinessRuleException("O Google recusou a autorização salva. Conecte a conta novamente.");
        }
        return payload.AccessToken;
    }

    private async Task<GoogleTokenResponse> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["redirect_uri"] = options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Post, GoogleOAuthTokenEndpoint) { Content = content }, cancellationToken);
        return await ReadPayloadAsync<GoogleTokenResponse>(response, cancellationToken);
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string accessToken, object? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        if (content is not null) request.Content = JsonContent.Create(content);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken, bool allowNotFound = false)
    {
        var client = httpClientFactory.CreateClient("GoogleCalendar");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.IsSuccessStatusCode || allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return response;

        response.Dispose();
        throw new BusinessRuleException("Não foi possível concluir a comunicação com o Google Calendar. Verifique a conexão da conta e tente novamente.");
    }

    private static async Task<T> ReadPayloadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return payload ?? throw new BusinessRuleException("O Google Calendar retornou uma resposta inválida. Tente novamente.");
    }

    private async Task MarkSuccessfulSyncAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var tracked = await db.GoogleCalendarConnections.SingleOrDefaultAsync(item => item.Id == connectionId, cancellationToken);
        if (tracked is null) return;
        tracked.LastSuccessfulSyncAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private GoogleEventRequest BuildAppointmentEvent(Appointment appointment, int durationMinutes)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(appointment.ScheduledDateTime, DateTimeKind.Utc), clinicClock.TimeZone);
        var localEnd = localStart.AddMinutes(durationMinutes);
        var offset = clinicClock.TimeZone.GetUtcOffset(localStart);
        var endOffset = clinicClock.TimeZone.GetUtcOffset(localEnd);
        var firstName = appointment.ClientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Cliente";
        return new GoogleEventRequest
        {
            Summary = $"{firstName} — {appointment.ProcedureName}",
            Description = "Agendamento criado pelo Painel Estética. Dados clínicos e financeiros permanecem no sistema.",
            Start = new GoogleEventDateTime { DateTime = new DateTimeOffset(localStart, offset).ToString("O"), TimeZone = clinicClock.TimeZone.Id },
            End = new GoogleEventDateTime { DateTime = new DateTimeOffset(localEnd, endOffset).ToString("O"), TimeZone = clinicClock.TimeZone.Id },
            ExtendedProperties = new GoogleExtendedProperties
            {
                Private = new Dictionary<string, string>
                {
                    [IntegrationMarker] = "appointment",
                    ["appointment-id"] = appointment.Id.ToString("N")
                }
            }
        };
    }

    private static bool IsWritable(string? accessRole) => accessRole is "owner" or "writer";

    private static bool IsInternalAppointment(GoogleEventResource item) =>
        item.ExtendedProperties?.Private?.TryGetValue(IntegrationMarker, out var marker) == true && marker == "appointment";

    private static GoogleCalendarEventDto? ToEventDto(GoogleEventResource item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || item.Start is null || item.End is null) return null;
        if (!string.IsNullOrWhiteSpace(item.Start.Date) && !string.IsNullOrWhiteSpace(item.End.Date))
        {
            if (!DateOnly.TryParse(item.Start.Date, out var start) || !DateOnly.TryParse(item.End.Date, out var end)) return null;
            return new GoogleCalendarEventDto(item.Id, item.Summary ?? "Evento sem título", start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), end.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), true, item.HtmlLink, item.ColorId);
        }
        if (!DateTime.TryParse(item.Start.DateTime, out var startDateTime) || !DateTime.TryParse(item.End.DateTime, out var endDateTime)) return null;
        return new GoogleCalendarEventDto(
            item.Id,
            item.Summary ?? "Evento sem título",
            startDateTime.ToUniversalTime(),
            endDateTime.ToUniversalTime(),
            false,
            item.HtmlLink,
            item.ColorId);
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }
    }

    private sealed class CalendarListResponse
    {
        public List<CalendarListItem> Items { get; init; } = [];
    }

    private sealed class CalendarListItem
    {
        public string? Id { get; init; }
        public string? Summary { get; init; }
        public bool Primary { get; init; }
        public string? AccessRole { get; init; }
        public string? BackgroundColor { get; init; }
    }

    private sealed class EventListResponse
    {
        public List<GoogleEventResource> Items { get; init; } = [];
    }

    private sealed class GoogleEventResource
    {
        public string? Id { get; init; }
        public string? Summary { get; init; }
        public string? Status { get; init; }
        public string? HtmlLink { get; init; }
        public string? ColorId { get; init; }
        public GoogleEventDateTime? Start { get; init; }
        public GoogleEventDateTime? End { get; init; }
        public GoogleExtendedProperties? ExtendedProperties { get; init; }
    }

    private sealed class GoogleEventRequest
    {
        public string Summary { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public GoogleEventDateTime Start { get; init; } = new();
        public GoogleEventDateTime End { get; init; } = new();
        public GoogleExtendedProperties ExtendedProperties { get; init; } = new();
    }

    private sealed class GoogleEventDateTime
    {
        public string? DateTime { get; init; }
        public string? Date { get; init; }
        public string? TimeZone { get; init; }
    }

    private sealed class GoogleExtendedProperties
    {
        public Dictionary<string, string> Private { get; init; } = [];
    }
}
