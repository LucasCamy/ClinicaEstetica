namespace PainelEstetica.Domain.Enums;

public enum AppointmentStatus
{
    Scheduled = 0,   // Agendado
    Confirmed = 1,   // Confirmado
    InProgress = 2,  // Em Atendimento
    Completed = 3,   // Concluído
    Cancelled = 4,   // Cancelado
    NoShow = 5       // Faltou
}
