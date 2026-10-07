using Microsoft.EntityFrameworkCore;
using PainelEstetica.Application.Clients;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Domain.Enums;
using PainelEstetica.Infrastructure.Data;

namespace PainelEstetica.Infrastructure.Services;

public sealed class ClientService(EsteticaDbContext db, IClock clock) : IClientService
{
    public async Task<PagedResult<ClientDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Clients.AsNoTracking();
        var search = page.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToLower();
            query = query.Where(client =>
                client.Name.ToLower().Contains(normalized) ||
                client.Phone.Contains(search) ||
                client.Cpf.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query
            .OrderByDescending(client => client.CreatedAt)
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientDto>(
            entities.Select(client => client.ToDto()).ToArray(),
            page.SafePage,
            page.SafePageSize,
            total);
    }

    public async Task<ClientDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return client?.ToDto();
    }

    public async Task<ClinicalClientDto?> GetClinicalAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients
            .AsNoTracking()
            .Include(item => item.MedicalRecords)
            .Include(item => item.Photos)
            .Include(item => item.Documents)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return client is null
            ? null
            : new ClinicalClientDto(
                client.ToDto(),
                client.MedicalRecords.OrderByDescending(record => record.SessionDate).Select(record => record.ToDto()).ToArray(),
                client.Photos.OrderByDescending(photo => photo.CreatedAt).Select(photo => photo.ToDto()).ToArray(),
                client.Documents.OrderByDescending(document => document.UploadedAt).Select(document => document.ToDto()).ToArray());
    }

    public async Task<ClientSummaryDto> GetSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(client => client.Id == id, cancellationToken))
        {
            throw new ResourceNotFoundException("Cliente", id);
        }

        var appointments = await db.Appointments
            .AsNoTracking()
            .Where(item => item.ClientId == id)
            .Select(item => new { item.Status, item.ScheduledDateTime, item.TotalPrice, item.AmountPaid })
            .ToListAsync(cancellationToken);
        var records = await db.MedicalRecords
            .AsNoTracking()
            .Where(item => item.ClientId == id)
            .Select(item => new { item.ProcedureName, item.SessionDate })
            .ToListAsync(cancellationToken);
        var documentsCount = await db.ClientDocuments.CountAsync(item => item.ClientId == id, cancellationToken);
        var photosCount = await db.ClientPhotos.CountAsync(item => item.ClientId == id, cancellationToken);
        var formCounts = await db.FormSubmissions
            .AsNoTracking()
            .Where(item => item.ClientId == id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Draft = group.Count(item => item.Status == FormSubmissionStatus.Draft),
                Finalized = group.Count(item => item.Status == FormSubmissionStatus.Finalized || item.Status == FormSubmissionStatus.Amended),
                Signed = group.Count(item => item.Signatures.Any())
            })
            .SingleOrDefaultAsync(cancellationToken);

        var now = clock.UtcNow;
        var billableAppointments = appointments.Where(item => item.Status != AppointmentStatus.Cancelled).ToArray();
        var upcomingAppointments = appointments
            .Where(item => (item.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed) && item.ScheduledDateTime >= now)
            .OrderBy(item => item.ScheduledDateTime)
            .ToArray();

        return new ClientSummaryDto(
            id,
            appointments.Count,
            appointments.Count(item => item.Status == AppointmentStatus.Completed),
            upcomingAppointments.Length,
            upcomingAppointments.FirstOrDefault()?.ScheduledDateTime,
            records.Count,
            records.OrderByDescending(item => item.SessionDate).Select(item => (DateTime?)item.SessionDate).FirstOrDefault(),
            records
                .GroupBy(item => item.ProcedureName)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(3)
                .Select(group => new ProcedureCountDto(group.Key, group.Count()))
                .ToArray(),
            documentsCount,
            photosCount,
            formCounts?.Draft ?? 0,
            formCounts?.Finalized ?? 0,
            formCounts?.Signed ?? 0,
            billableAppointments.Sum(item => item.TotalPrice),
            billableAppointments.Sum(item => item.AmountPaid),
            billableAppointments.Sum(item => Math.Max(0m, item.TotalPrice - item.AmountPaid)));
    }

    public async Task<ClientDto> CreateAsync(SaveClientRequest request, CancellationToken cancellationToken)
    {
        await EnsureCpfIsAvailableAsync(request.Cpf, null, cancellationToken);

        var client = new Client
        {
            Id = Guid.NewGuid(),
            CreatedAt = clock.UtcNow
        };
        Apply(client, request);
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return client.ToDto();
    }

    public async Task<ClientDto> UpdateAsync(Guid id, SaveClientRequest request, CancellationToken cancellationToken)
    {
        var client = await db.Clients.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                     ?? throw new ResourceNotFoundException("Cliente", id);
        await EnsureCpfIsAvailableAsync(request.Cpf, id, cancellationToken);
        Apply(client, request);
        await db.SaveChangesAsync(cancellationToken);
        return client.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                     ?? throw new ResourceNotFoundException("Cliente", id);
        client.IsDeleted = true;
        client.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MedicalRecordDto> CreateMedicalRecordAsync(
        Guid clientId,
        CreateMedicalRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(client => client.Id == clientId, cancellationToken))
        {
            throw new ResourceNotFoundException("Cliente", clientId);
        }

        if (request.AppointmentId.HasValue &&
            !await db.Appointments.AnyAsync(
                appointment => appointment.Id == request.AppointmentId && appointment.ClientId == clientId,
                cancellationToken))
        {
            throw new BusinessRuleException("O agendamento informado não pertence ao cliente.");
        }

        var record = new MedicalRecord
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            AppointmentId = request.AppointmentId,
            ProcedureName = request.ProcedureName.Trim(),
            TreatedArea = request.TreatedArea.Trim(),
            ParametersUsed = request.ParametersUsed.Trim(),
            ClinicalNotes = request.ClinicalNotes.Trim(),
            PostCareInstructions = request.PostCareInstructions.Trim(),
            SessionDate = request.SessionDate ?? clock.UtcNow,
            CreatedAt = clock.UtcNow
        };

        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return record.ToDto();
    }

    private async Task EnsureCpfIsAvailableAsync(string cpf, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalized = cpf.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return;
        }

        if (await db.Clients.AnyAsync(
                client => client.Cpf == normalized && client.Id != currentId,
                cancellationToken))
        {
            throw new ConflictException("Já existe um cliente ativo com este CPF.");
        }
    }

    private static void Apply(Client client, SaveClientRequest request)
    {
        client.Name = request.Name.Trim();
        client.Email = request.Email.Trim();
        client.Phone = request.Phone.Trim();
        client.Cpf = request.Cpf.Trim();
        client.Rg = request.Rg.Trim();
        client.BirthDate = request.BirthDate;
        client.Profession = request.Profession.Trim();
        client.Source = request.Source;
        client.Address = request.Address?.Trim();
        client.City = request.City?.Trim();
        client.State = request.State?.Trim().ToUpperInvariant();
        client.Notes = request.Notes?.Trim();
    }
}
