using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Auditing;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class AuditService(EsteticaDbContext db, IClock clock) : IAuditService
{
    public async Task WriteAsync(
        AuditContext context,
        string action,
        string resourceType,
        string? resourceId = null,
        string outcome = "Success",
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var detailsJson = details is null ? null : JsonSerializer.Serialize(details);
        if (detailsJson?.Length > 4000)
        {
            detailsJson = detailsJson[..4000];
        }

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = context.ActorUserId,
            ActorUsername = string.IsNullOrWhiteSpace(context.ActorUsername) ? "anonymous" : context.ActorUsername,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Outcome = outcome,
            IpAddress = context.IpAddress,
            UserAgent = context.UserAgent,
            DetailsJson = detailsJson,
            OccurredAtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditEventDto>> ListAsync(
        PageRequest page,
        string? action,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var query = db.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(item => item.Action == action.Trim());
        if (actorUserId.HasValue) query = query.Where(item => item.ActorUserId == actorUserId.Value);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var search = page.Search.Trim().ToLower();
            query = query.Where(item =>
                item.ActorUsername.ToLower().Contains(search) ||
                item.ResourceType.ToLower().Contains(search) ||
                (item.ResourceId != null && item.ResourceId.ToLower().Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(item => item.OccurredAtUtc)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);
        var items = entities.Select(item => new AuditEventDto(
            item.Id,
            item.ActorUserId,
            item.ActorUsername,
            item.Action,
            item.ResourceType,
            item.ResourceId,
            item.Outcome,
            item.IpAddress,
            item.DetailsJson,
            item.OccurredAtUtc)).ToArray();
        return new PagedResult<AuditEventDto>(items, page.SafePage, page.SafePageSize, total);
    }
}
