namespace PainelEstetica.Domain.Enums;

public enum FinancialEntryType
{
    Income = 0,
    Expense = 1
}

public enum FinancialEntryStatus
{
    Planned = 0,
    Settled = 1,
    Cancelled = 2
}

public enum FinancialLedgerSource
{
    AppointmentReceipt = 0,
    ManualEntry = 1
}
