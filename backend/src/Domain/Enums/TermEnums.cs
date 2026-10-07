namespace PainelEstetica.Domain.Enums;

public enum TermTemplateStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum TermFieldType
{
    Text = 0,
    Date = 1,
    Checkbox = 2,
    Handwriting = 3,
    Signature = 4
}

public enum TermSubmissionStatus
{
    Draft = 0,
    Finalized = 1,
    Voided = 2
}
