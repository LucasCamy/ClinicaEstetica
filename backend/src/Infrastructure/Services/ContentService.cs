using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Content;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class ContentService(EsteticaDbContext db) : IContentService
{
    private const int MaxCarouselItems = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CmsContentDto> GetAsync(CancellationToken cancellationToken)
    {
        var content = await db.CmsContents.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                      ?? throw new ResourceNotFoundException("Conteúdo da landing page", "default");
        return content.ToDto();
    }

    public async Task<CmsContentDto> UpdateAsync(UpdateCmsRequest request, CancellationToken cancellationToken)
    {
        var content = await db.CmsContents.FirstOrDefaultAsync(cancellationToken);
        if (content is null)
        {
            content = new CmsContent { Id = Guid.NewGuid() };
            db.CmsContents.Add(content);
        }

        content.HeroTitle = NormalizeText(request.HeroTitle);
        content.HeroSubtitle = NormalizeText(request.HeroSubtitle);
        content.DoctorName = NormalizeText(request.DoctorName);
        content.DoctorTitle = NormalizeText(request.DoctorTitle);
        content.DoctorBio = NormalizeText(request.DoctorBio);
        content.DoctorPhotoUrl = NormalizeImageUrl(request.DoctorPhotoUrl);
        content.LogoOnDarkUrl = NormalizeImageUrl(request.LogoOnDarkUrl);
        content.LogoOnLightUrl = NormalizeImageUrl(request.LogoOnLightUrl);
        content.WhatsappNumber = NormalizeText(request.WhatsappNumber);
        content.AddressText = NormalizeText(request.AddressText);
        content.PublicHoursWeekdays = NormalizeText(request.PublicHoursWeekdays);
        content.PublicHoursSaturday = NormalizeText(request.PublicHoursSaturday);
        content.PublicHoursNote = NormalizeText(request.PublicHoursNote);
        content.AboutTitle = NormalizeText(request.AboutTitle);
        content.AboutText = NormalizeText(request.AboutText);
        content.AboutImageUrl = NormalizeImageUrl(request.AboutImageUrl);
        content.ClinicTitle = NormalizeText(request.ClinicTitle);
        content.ClinicText = NormalizeText(request.ClinicText);
        content.ClinicImageUrl = NormalizeImageUrl(request.ClinicImageUrl);
        content.CarouselItemsJson = JsonSerializer.Serialize(NormalizeCarouselItems(request.CarouselItems), JsonOptions);
        await db.SaveChangesAsync(cancellationToken);
        return content.ToDto();
    }

    private static IReadOnlyList<LandingCarouselItemDto> NormalizeCarouselItems(
        IReadOnlyList<LandingCarouselItemRequest>? requestedItems)
    {
        var items = requestedItems ?? [];
        if (items.Count > MaxCarouselItems)
        {
            throw new BusinessRuleException($"A galeria aceita no máximo {MaxCarouselItems} imagens.");
        }

        var normalized = new List<LandingCarouselItemDto>(items.Count);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new BusinessRuleException("A galeria contém um item inválido.");
            }

            var imageUrl = NormalizeText(item.ImageUrl);
            var title = NormalizeText(item.Title);
            var description = NormalizeText(item.Description);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(description))
                {
                    throw new BusinessRuleException("Inclua uma imagem ou remova o item vazio da galeria.");
                }
                continue;
            }

            EnsureMaxLength(item.Id, 80, "identificador da imagem");
            EnsureMaxLength(imageUrl, 500, "link da imagem");
            EnsureMaxLength(title, 160, "título da imagem");
            EnsureMaxLength(description, 600, "descrição da imagem");

            normalized.Add(new LandingCarouselItemDto(
                string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : NormalizeText(item.Id),
                NormalizeImageUrl(imageUrl),
                title,
                description));
        }

        return normalized;
    }

    private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

    private static string NormalizeImageUrl(string? value)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;
        if (!IsAllowedImageUrl(normalized))
        {
            throw new BusinessRuleException("As imagens devem usar HTTPS, a biblioteca enviada ou os arquivos da identidade visual.");
        }
        return normalized;
    }

    private static void EnsureMaxLength(string? value, int maxLength, string field)
    {
        if (value?.Length > maxLength)
        {
            throw new BusinessRuleException($"O {field} excede o limite de {maxLength} caracteres.");
        }
    }

    private static bool IsAllowedImageUrl(string value) =>
        value.StartsWith("/api/public/cms/", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/brand/", StringComparison.OrdinalIgnoreCase) ||
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
