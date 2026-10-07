using PainelEstetica.Application.Common;

namespace PainelEstetica.Application.Auditing;

public sealed record AuditContext(
    Guid? ActorUserId,
    string ActorUsername,
    string? IpAddress,
    string? UserAgent);

public sealed record AuditEventDto(
    Guid Id,
    Guid? ActorUserId,
    string ActorUsername,
    string Action,
    string ResourceType,
    string? ResourceId,
    string Outcome,
    string? IpAddress,
    string? DetailsJson,
    DateTime OccurredAtUtc);

public interface IAuditService
{
    Task WriteAsync(
        AuditContext context,
        string action,
        string resourceType,
        string? resourceId = null,
        string outcome = "Success",
        object? details = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AuditEventDto>> ListAsync(
        PageRequest page,
        string? action,
        Guid? actorUserId,
        CancellationToken cancellationToken);
}
