using System.ComponentModel.DataAnnotations;

namespace PainelEstetica.Application.Content;

public sealed record CmsContentDto(
    Guid Id,
    string HeroTitle,
    string HeroSubtitle,
    string DoctorName,
    string DoctorTitle,
    string DoctorBio,
    string DoctorPhotoUrl,
    string LogoOnDarkUrl,
    string LogoOnLightUrl,
    string WhatsappNumber,
    string AddressText,
    string PublicHoursWeekdays,
    string PublicHoursSaturday,
    string PublicHoursNote,
    string AboutTitle,
    string AboutText,
    string AboutImageUrl,
    string ClinicTitle,
    string ClinicText,
    string ClinicImageUrl,
    IReadOnlyList<LandingCarouselItemDto> CarouselItems);

public sealed record LandingCarouselItemDto(
    string Id,
    string ImageUrl,
    string Title,
    string Description);

public sealed record CmsImageUploadDto(string Url);

public sealed class UpdateCmsRequest
{
    [Required, StringLength(240)]
    public string HeroTitle { get; init; } = string.Empty;

    [Required, StringLength(500)]
    public string HeroSubtitle { get; init; } = string.Empty;

    [Required, StringLength(160)]
    public string DoctorName { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string DoctorTitle { get; init; } = string.Empty;

    [Required, StringLength(3000)]
    public string DoctorBio { get; init; } = string.Empty;

    [StringLength(500)]
    public string DoctorPhotoUrl { get; init; } = string.Empty;

    [StringLength(500)]
    public string LogoOnDarkUrl { get; init; } = string.Empty;

    [StringLength(500)]
    public string LogoOnLightUrl { get; init; } = string.Empty;

    [Required, StringLength(30)]
    public string WhatsappNumber { get; init; } = string.Empty;

    [StringLength(300)]
    public string AddressText { get; init; } = string.Empty;

    [StringLength(120)]
    public string PublicHoursWeekdays { get; init; } = string.Empty;

    [StringLength(120)]
    public string PublicHoursSaturday { get; init; } = string.Empty;

    [StringLength(240)]
    public string PublicHoursNote { get; init; } = string.Empty;

    [StringLength(160)]
    public string AboutTitle { get; init; } = string.Empty;

    [StringLength(3000)]
    public string AboutText { get; init; } = string.Empty;

    [StringLength(500)]
    public string AboutImageUrl { get; init; } = string.Empty;

    [StringLength(160)]
    public string ClinicTitle { get; init; } = string.Empty;

    [StringLength(3000)]
    public string ClinicText { get; init; } = string.Empty;

    [StringLength(500)]
    public string ClinicImageUrl { get; init; } = string.Empty;

    public IReadOnlyList<LandingCarouselItemRequest> CarouselItems { get; init; } = [];
}

public sealed class LandingCarouselItemRequest
{
    [StringLength(80)]
    public string Id { get; init; } = string.Empty;

    [StringLength(500)]
    public string ImageUrl { get; init; } = string.Empty;

    [StringLength(160)]
    public string Title { get; init; } = string.Empty;

    [StringLength(600)]
    public string Description { get; init; } = string.Empty;
}

public interface IContentService
{
    Task<CmsContentDto> GetAsync(CancellationToken cancellationToken);
    Task<CmsContentDto> UpdateAsync(UpdateCmsRequest request, CancellationToken cancellationToken);
}
