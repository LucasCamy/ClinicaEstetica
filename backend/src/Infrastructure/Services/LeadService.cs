using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Leads;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class LeadService(EsteticaDbContext db, IClock clock) : ILeadService
{
    public async Task<LeadDto> CreatePublicAsync(CreateLeadRequest request, CancellationToken cancellationToken)
    {
        // Se o campo honeypot estiver preenchido, é um bot; retorna DTO aceito sem persistir
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return new LeadDto(
                Guid.NewGuid(),
                request.Name.Trim(),
                request.Phone.Trim(),
                request.Email?.Trim() ?? string.Empty,
                request.InterestedProcedure?.Trim() ?? string.Empty,
                request.Source ?? LeadSource.Other,
                "Novo",
                request.Message?.Trim(),
                clock.UtcNow);
        }

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            Email = request.Email?.Trim() ?? string.Empty,
            InterestedProcedure = request.InterestedProcedure?.Trim() ?? string.Empty,
            Message = request.Message?.Trim(),
            Source = request.Source ?? LeadSource.Other,
            Status = "Novo",
            CreatedAt = clock.UtcNow
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync(cancellationToken);
        return lead.ToDto();
    }

    public async Task<PagedResult<LeadDto>> ListAsync(
        PageRequest page,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = db.Leads.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(lead => lead.Status == status.Trim());
        }

        var search = page.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLower();
            query = query.Where(lead =>
                lead.Name.ToLower().Contains(normalized) ||
                lead.Phone.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(lead => lead.CreatedAt)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<LeadDto>(
            entities.Select(item => item.ToDto()).ToArray(),
            page.SafePage,
            page.SafePageSize,
            total);
    }

    public async Task<LeadDto> UpdateStatusAsync(
        Guid id,
        UpdateLeadStatusRequest request,
        CancellationToken cancellationToken)
    {
        var status = request.Status.Trim();
        if (!LeadStatuses.All.Contains(status))
        {
            throw new ArgumentException("Status de contato inválido.");
        }

        var lead = await db.Leads.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                   ?? throw new ResourceNotFoundException("Contato", id);
        lead.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        return lead.ToDto();
    }
}
