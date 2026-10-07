using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using PainelEstetica.Application.Appointments;
using PainelEstetica.Application.Common;
using PainelEstetica.Application.Settings;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Data;
using Xunit;

namespace PainelEstetica.IntegrationTests;

public sealed class ClinicOperationalSettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Operational_request_accepts_times_from_html_time_input()
    {
        const string json = """
        { "operatingHours": [
          { "dayOfWeek": 0, "isOpen": true, "startTime": "07:00", "endTime": "19:00" },
          { "dayOfWeek": 1, "isOpen": true, "startTime": "07:00", "endTime": "19:00" },
          { "dayOfWeek": 2, "isOpen": true, "startTime": "07:00", "endTime": "19:00" },
          { "dayOfWeek": 3, "isOpen": true, "startTime": "07:00", "endTime": "19:00" },
          { "dayOfWeek": 4, "isOpen": true, "startTime": "07:00", "endTime": "19:00" },
          { "dayOfWeek": 5, "isOpen": true, "startTime": "07:00", "endTime": "19:00" },
          { "dayOfWeek": 6, "isOpen": true, "startTime": "07:00", "endTime": "19:00" }
        ] }
        """;

        var request = JsonSerializer.Deserialize<SaveOperatingHoursRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);
        Assert.Equal(7, request.OperatingHours.Count);
        Assert.Equal(new TimeOnly(7, 0), request.OperatingHours[0].StartTime);
    }

    [Fact]
    public async Task New_appointment_outside_expedient_is_rejected_but_existing_one_can_be_edited()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EsteticaDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<IClinicSettingsService>();
        var appointments = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClinicClock>();

        var operatingHours = Enumerable.Range(0, 7)
            .Select(day => new OperatingHoursDayDto(day, true, new TimeOnly(7, 0), new TimeOnly(19, 0)))
            .ToArray();
        await settings.UpdateOperatingHoursAsync(operatingHours, CancellationToken.None);

        var client = new Client { Name = "Cliente de horário", Email = "horario@example.invalid", Phone = "65999999999", Cpf = "12345678901", Rg = "123456" };
        var procedure = new ProcedureType { Name = "Procedimento de horário", Description = "Teste", Price = 100, DurationMinutes = 30 };
        db.Clients.Add(client);
        db.ProcedureTypes.Add(procedure);
        await db.SaveChangesAsync();

        // 06:30 no fuso America/Cuiaba = 10:30 UTC: fora do expediente recém configurado.
        var outsideTime = new DateTime(2026, 8, 10, 10, 30, 0, DateTimeKind.Utc);
        var request = BuildRequest(client, procedure, outsideTime, "original");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => appointments.CreateAsync(request, CancellationToken.None));
        Assert.Contains("fora do expediente", error.Message, StringComparison.OrdinalIgnoreCase);

        var previous = new Appointment
        {
            ClientId = client.Id,
            ClientName = client.Name,
            ProcedureTypeId = procedure.Id,
            ProcedureName = procedure.Name,
            ScheduledDateTime = outsideTime,
            TotalPrice = 100,
            AmountPaid = 0,
            ProfessionalName = "Profissional de teste",
            CreatedAt = clock.UtcNow,
        };
        db.Appointments.Add(previous);
        await db.SaveChangesAsync();

        var updated = await appointments.UpdateAsync(previous.Id, BuildRequest(client, procedure, outsideTime, "observação revisada"), CancellationToken.None);
        Assert.Equal("observação revisada", updated.Notes);
        Assert.Equal(outsideTime, updated.ScheduledDateTime);
    }

    private static SaveAppointmentRequest BuildRequest(Client client, ProcedureType procedure, DateTime scheduledAt, string notes) => new()
    {
        ClientId = client.Id,
        ProcedureTypeId = procedure.Id,
        ScheduledDateTime = scheduledAt,
        TotalPrice = 100,
        AmountPaid = 0,
        ProfessionalName = "Profissional de teste",
        Notes = notes,
    };
}
