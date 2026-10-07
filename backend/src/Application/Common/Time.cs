namespace PainelEstetica.Application.Common;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class ClinicOptions
{
    public const string SectionName = "Clinic";

    public string TimeZoneId { get; set; } = "America/Cuiaba";
}

public interface IClinicClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly LocalToday { get; }
    TimeZoneInfo TimeZone { get; }
}

public sealed class ClinicClock(IClock clock, ClinicOptions options) : IClinicClock
{
    public DateTime UtcNow => clock.UtcNow;

    public TimeZoneInfo TimeZone { get; } = Resolve(options.TimeZoneId);

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(UtcNow, DateTimeKind.Utc),
        TimeZone);

    public DateOnly LocalToday => DateOnly.FromDateTime(LocalNow);

    private static TimeZoneInfo Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Clinic:TimeZoneId é obrigatório.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new InvalidOperationException($"Fuso horário da clínica inválido: {id}.", exception);
        }
    }
}
