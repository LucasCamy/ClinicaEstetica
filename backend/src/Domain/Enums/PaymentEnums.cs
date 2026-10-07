namespace PainelEstetica.Domain.Enums;

public enum PaymentMethod
{
    Pix = 0,
    CreditCard = 1,
    DebitCard = 2,
    Cash = 3,
    PackageSession = 4
}

public enum PaymentStatus
{
    Paid = 0,
    Partial = 1,
    Pending = 2
}

public enum LeadSource
{
    Instagram = 0,
    Indication = 1,
    GoogleAds = 2,
    WhatsApp = 3,
    WalkIn = 4,
    Other = 5,
    Website = 6
}

public enum PhotoType
{
    Before = 0,
    Progress = 1,
    After = 2
}
