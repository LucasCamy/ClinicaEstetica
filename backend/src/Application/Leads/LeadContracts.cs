using System.ComponentModel.DataAnnotations;
using PainelEstetica.Application.Common;
using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Application.Leads;

public sealed record LeadDto(
    Guid Id,
    string Name,
    string Phone,
    string Email,
    string InterestedProcedure,
    LeadSource Source,
    string Status,
    string? Message,
    DateTime CreatedAt);

public sealed class CreateLeadRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 8)]
    public string Phone { get; init; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string? Email { get; init; }

    [StringLength(160)]
    public string? InterestedProcedure { get; init; }

    [StringLength(2000)]
    public string? Message { get; init; }

    public LeadSource? Source { get; init; }

    /// <summary>
    /// Campo honeypot oculto na interface para capturar e descartar robôs/spammers.
    /// </summary>
    [StringLength(100)]
    public string? Website { get; init; }
}

public static class LeadStatuses
{
    public const string New = "Novo";
    public const string InContact = "Em contato";
    public const string Scheduled = "Avaliação agendada";
    public const string Converted = "Convertido";
    public const string Closed = "Encerrado";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        New,
        InContact,
        Scheduled,
        Converted,
        Closed
    };
}

public sealed class UpdateLeadStatusRequest
{
    [Required, StringLength(40)]
    public string Status { get; init; } = string.Empty;
}

public interface ILeadService
{
    Task<LeadDto> CreatePublicAsync(CreateLeadRequest request, CancellationToken cancellationToken);
    Task<PagedResult<LeadDto>> ListAsync(PageRequest page, string? status, CancellationToken cancellationToken);
    Task<LeadDto> UpdateStatusAsync(Guid id, UpdateLeadStatusRequest request, CancellationToken cancellationToken);
}
