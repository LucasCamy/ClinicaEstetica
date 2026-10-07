namespace PainelEstetica.Domain.Enums;

public enum FormTemplateStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

public enum FormFieldType
{
    Section = 0,
    InformationalText = 1,
    ShortText = 2,
    LongText = 3,
    Number = 4,
    Date = 5,
    YesNo = 6,
    Checkbox = 7,
    CheckboxGroup = 8,
    Dropdown = 9,
    MultiSelect = 10,
    Signature = 11
}

public enum FormSubmissionStatus
{
    Draft = 0,
    Finalized = 1,
    Amended = 2,
    Voided = 3
}

public enum FormSignatureMethod
{
    InPersonDrawn = 0
}
