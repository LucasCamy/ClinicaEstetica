using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PainelEstetica.Application.Settings;

public sealed record OperatingHoursDayDto(
    int DayOfWeek,
    bool IsOpen,
    [property: JsonConverter(typeof(HtmlTimeOnlyJsonConverter))]
    TimeOnly StartTime,
    [property: JsonConverter(typeof(HtmlTimeOnlyJsonConverter))]
    TimeOnly EndTime);

public sealed class HtmlTimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] AcceptedFormats = ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF"];

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (!string.IsNullOrWhiteSpace(value) && TimeOnly.TryParseExact(
                value,
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            return time;
        }

        throw new JsonException("O horário deve estar no formato HH:mm.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
}

public sealed record ClinicOperationalSettingsDto(
    string TimeZoneId,
    IReadOnlyList<OperatingHoursDayDto> OperatingHours,
    DateTime UpdatedAtUtc);

public sealed class SaveOperatingHoursRequest
{
    [Required]
    [MinLength(7)]
    [MaxLength(7)]
    public List<OperatingHoursDayDto> OperatingHours { get; init; } = [];
}

public sealed record StorageUsageDto(
    long UsedBytes,
    long LimitBytes);

public interface IClinicSettingsService
{
    Task<ClinicOperationalSettingsDto> GetOperationalSettingsAsync(CancellationToken cancellationToken);
    Task<ClinicOperationalSettingsDto> UpdateOperatingHoursAsync(
        IReadOnlyList<OperatingHoursDayDto> operatingHours,
        CancellationToken cancellationToken);
    Task<bool> IsWithinOperatingHoursAsync(DateTime scheduledAtUtc, CancellationToken cancellationToken);
}
