using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Catalog;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class ProcedureService(EsteticaDbContext db) : IProcedureService
{
    public async Task<IReadOnlyList<ProcedureDto>> ListAsync(bool publicOnly, CancellationToken cancellationToken)
    {
        var query = db.ProcedureTypes.AsNoTracking();
        if (publicOnly)
        {
            query = query.Where(procedure => procedure.IsPublicWebsite && procedure.IsActive);
        }

        var items = await query.OrderBy(procedure => procedure.Name).ToListAsync(cancellationToken);
        return items.Select(item => item.ToDto()).ToArray();
    }

    public Task<ProcedureDto> CreateAsync(SaveProcedureRequest request, CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<ProcedureDto> UpdateAsync(Guid id, SaveProcedureRequest request, CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var procedure = await db.ProcedureTypes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                        ?? throw new ResourceNotFoundException("Procedimento", id);
        procedure.IsActive = false;
        procedure.IsPublicWebsite = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProcedureDto> SaveAsync(
        Guid? id,
        SaveProcedureRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim().ToLower();
        if (await db.ProcedureTypes.AnyAsync(
                item => item.Name.ToLower() == normalizedName && item.Id != id,
                cancellationToken))
        {
            throw new ConflictException("Já existe um procedimento com este nome.");
        }

        var procedure = id.HasValue
            ? await db.ProcedureTypes.SingleOrDefaultAsync(item => item.Id == id.Value, cancellationToken)
              ?? throw new ResourceNotFoundException("Procedimento", id.Value)
            : new ProcedureType { Id = Guid.NewGuid() };

        procedure.Name = request.Name.Trim();
        procedure.Description = request.Description.Trim();
        procedure.Price = request.Price;
        procedure.DurationMinutes = request.DurationMinutes;
        procedure.ImageUrl = request.ImageUrl.Trim();
        procedure.IsPublicWebsite = request.IsPublicWebsite;
        procedure.IsPriceHiddenOnWebsite = request.IsPriceHiddenOnWebsite;
        procedure.IsActive = request.IsActive;
        procedure.IsFeaturedInCarousel = request.IsFeaturedInCarousel;
        procedure.RecommendedReturnDays = request.RecommendedReturnDays;

        if (!id.HasValue) db.ProcedureTypes.Add(procedure);
        await db.SaveChangesAsync(cancellationToken);
        return procedure.ToDto();
    }
}
