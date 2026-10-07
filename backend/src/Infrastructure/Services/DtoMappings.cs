using PainelEstetica.Application.Appointments;
using PainelEstetica.Application.Catalog;
using PainelEstetica.Application.Clients;
using PainelEstetica.Application.Content;
using PainelEstetica.Application.Leads;
using PainelEstetica.Domain.Entities;

using System.Text.Json;

namespace PainelEstetica.Infrastructure.Services;

public static class DtoMappings
{
    public static ClientDto ToDto(this Client client) => new(
        client.Id,
        client.Name,
        client.Email,
        client.Phone,
        client.Cpf,
        client.Rg,
        client.BirthDate,
        client.Profession,
        client.Source,
        client.Address,
        client.City,
        client.State,
        client.Notes,
        client.IsActive,
        client.CreatedAt);

    public static MedicalRecordDto ToDto(this MedicalRecord record) => new(
        record.Id,
        record.ClientId,
        record.AppointmentId,
        record.ProcedureName,
        record.TreatedArea,
        record.ParametersUsed,
        record.ClinicalNotes,
        record.PostCareInstructions,
        record.SessionDate,
        record.CreatedAt);

    public static ClientPhotoDto ToDto(this ClientPhoto photo) => new(
        photo.Id,
        photo.ClientId,
        photo.Type,
        photo.ProcedureName,
        photo.IsPublicForWebsite,
        photo.ConsentGiven,
        photo.CreatedAt,
        photo.OriginalFileSizeBytes,
        photo.FileSizeBytes,
        photo.Width,
        photo.Height,
        $"/api/clients/{photo.ClientId}/photos/{photo.Id}/content");

    public static ClientDocumentDto ToDto(this ClientDocument document) => new(
        document.Id,
        document.ClientId,
        document.FileName,
        document.DocumentType,
        document.UploadedAt,
        document.ContentType,
        document.FileSizeBytes,
        $"/api/clients/{document.ClientId}/documents/{document.Id}/content");

    public static AppointmentDto ToDto(this Appointment appointment) => new(
        appointment.Id,
        appointment.ClientId,
        appointment.ClientName,
        appointment.ProcedureTypeId,
        appointment.ProcedureName,
        appointment.ScheduledDateTime,
        appointment.Status,
        appointment.TotalPrice,
        appointment.AmountPaid,
        appointment.PaymentMethod,
        appointment.PaymentStatus,
        appointment.ProfessionalName,
        appointment.Notes,
        appointment.CompletedAtUtc,
        appointment.CreatedAt);

    public static ProcedureDto ToDto(this ProcedureType procedure) => new(
        procedure.Id,
        procedure.Name,
        procedure.Description,
        procedure.Price,
        procedure.DurationMinutes,
        procedure.ImageUrl,
        procedure.IsPublicWebsite,
        procedure.IsPriceHiddenOnWebsite,
        procedure.IsActive,
        procedure.IsFeaturedInCarousel,
        procedure.RecommendedReturnDays);

    public static LeadDto ToDto(this Lead lead) => new(
        lead.Id,
        lead.Name,
        lead.Phone,
        lead.Email,
        lead.InterestedProcedure,
        lead.Source,
        lead.Status,
        lead.Message,
        lead.CreatedAt);

    public static CmsContentDto ToDto(this CmsContent content) => new(
        content.Id,
        content.HeroTitle,
        content.HeroSubtitle,
        content.DoctorName,
        content.DoctorTitle,
        content.DoctorBio,
        content.DoctorPhotoUrl,
        content.LogoOnDarkUrl,
        content.LogoOnLightUrl,
        content.WhatsappNumber,
        content.AddressText,
        content.PublicHoursWeekdays,
        content.PublicHoursSaturday,
        content.PublicHoursNote,
        content.AboutTitle,
        content.AboutText,
        content.AboutImageUrl,
        content.ClinicTitle,
        content.ClinicText,
        content.ClinicImageUrl,
        DeserializeCarouselItems(content.CarouselItemsJson));

    private static IReadOnlyList<LandingCarouselItemDto> DeserializeCarouselItems(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<LandingCarouselItemDto>>(
                value,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
