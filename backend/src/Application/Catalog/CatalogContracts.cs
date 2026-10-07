using System.ComponentModel.DataAnnotations;

namespace PainelEstetica.Application.Catalog;

public sealed record ProcedureDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int DurationMinutes,
    string ImageUrl,
    bool IsPublicWebsite,
    bool IsPriceHiddenOnWebsite,
    bool IsActive,
    bool IsFeaturedInCarousel,
    int? RecommendedReturnDays);

public sealed class SaveProcedureRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(2000)]
    public string Description { get; init; } = string.Empty;

    [Range(0, 9999999999)]
    public decimal Price { get; init; }

    [Range(5, 1440)]
    public int DurationMinutes { get; init; }

    [StringLength(500)]
    public string ImageUrl { get; init; } = string.Empty;

    public bool IsPublicWebsite { get; init; } = true;
    public bool IsPriceHiddenOnWebsite { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsFeaturedInCarousel { get; init; } = true;

    [Range(1, 3650)]
    public int? RecommendedReturnDays { get; init; }
}

public interface IProcedureService
{
    Task<IReadOnlyList<ProcedureDto>> ListAsync(bool publicOnly, CancellationToken cancellationToken);
    Task<ProcedureDto> CreateAsync(SaveProcedureRequest request, CancellationToken cancellationToken);
    Task<ProcedureDto> UpdateAsync(Guid id, SaveProcedureRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
