using System.Security.Claims;
using PainelEstetica.Application.Auditing;

namespace PainelEstetica.WebAPI.Endpoints;

public static class EndpointSecurityExtensions
{
    public static RouteHandlerBuilder Mutating(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<AntiforgeryFilter>();

    public static AuditContext ToAuditContext(this HttpContext context)
    {
        var idValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return new AuditContext(
            Guid.TryParse(idValue, out var id) ? id : null,
            context.User.Identity?.Name ?? "anonymous",
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString());
    }
}
