using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Settings;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class ClinicSettingsService(
    EsteticaDbContext db,
    IClinicClock clock) : IClinicSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ClinicOperationalSettingsDto> GetOperationalSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await db.ClinicOperationalSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var operatingHours = DeserializeOrDefault(settings?.OperatingHoursJson);
        return ToDto(operatingHours, settings?.UpdatedAtUtc ?? DateTime.UnixEpoch);
    }

    public async Task<ClinicOperationalSettingsDto> UpdateOperatingHoursAsync(
        IReadOnlyList<OperatingHoursDayDto> operatingHours,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeAndValidate(operatingHours);
        var settings = await db.ClinicOperationalSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new ClinicOperationalSettings { Id = Guid.NewGuid() };
            db.ClinicOperationalSettings.Add(settings);
        }

        settings.OperatingHoursJson = JsonSerializer.Serialize(normalized, SerializerOptions);
        settings.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(normalized, settings.UpdatedAtUtc);
    }

    public async Task<bool> IsWithinOperatingHoursAsync(DateTime scheduledAtUtc, CancellationToken cancellationToken)
    {
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(scheduledAtUtc, DateTimeKind.Utc),
            clock.TimeZone);
        var settings = await db.ClinicOperationalSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        var day = DeserializeOrDefault(settings?.OperatingHoursJson)
            .Single(item => item.DayOfWeek == (int)localDateTime.DayOfWeek);

        return day.IsOpen && localDateTime.TimeOfDay >= day.StartTime.ToTimeSpan() && localDateTime.TimeOfDay < day.EndTime.ToTimeSpan();
    }

    private ClinicOperationalSettingsDto ToDto(IReadOnlyList<OperatingHoursDayDto> operatingHours, DateTime updatedAtUtc) =>
        new(clock.TimeZone.Id, operatingHours, updatedAtUtc);

    private static List<OperatingHoursDayDto> DeserializeOrDefault(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<OperatingHoursDayDto>>(json, SerializerOptions);
                if (parsed is not null) return NormalizeAndValidate(parsed);
            }
            catch (JsonException)
            {
                // Uma configuração inválida não pode derrubar a agenda; o padrão será usado e poderá ser salvo novamente.
            }
        }

        return DefaultOperatingHours();
    }

    private static List<OperatingHoursDayDto> NormalizeAndValidate(IReadOnlyList<OperatingHoursDayDto> operatingHours)
    {
        if (operatingHours.Count != 7 || operatingHours.Select(item => item.DayOfWeek).Distinct().Count() != 7 || operatingHours.Any(item => item.DayOfWeek is < 0 or > 6))
        {
            throw new ArgumentException("Informe o horário de operação para cada dia da semana.");
        }

        if (operatingHours.Any(item => item.IsOpen && item.StartTime >= item.EndTime))
        {
            throw new ArgumentException("O horário de início deve ser anterior ao horário de término.");
        }

        return operatingHours
            .OrderBy(item => item.DayOfWeek)
            .Select(item => new OperatingHoursDayDto(item.DayOfWeek, item.IsOpen, item.StartTime, item.EndTime))
            .ToList();
    }

    private static List<OperatingHoursDayDto> DefaultOperatingHours() =>
        Enumerable.Range(0, 7)
            .Select(day => new OperatingHoursDayDto(day, true, new TimeOnly(7, 0), new TimeOnly(21, 0)))
            .ToList();
}
