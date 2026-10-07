using PainelEstetica.Domain.Enums;

namespace PainelEstetica.Domain.Entities;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Rg { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string Profession { get; set; } = string.Empty;
    public LeadSource Source { get; set; } = LeadSource.Instagram;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Appointment> Appointments { get; set; } = new();
    public List<MedicalRecord> MedicalRecords { get; set; } = new();
    public List<ClientPhoto> Photos { get; set; } = new();
    public List<ClientDocument> Documents { get; set; } = new();
    public List<FormSubmission> FormSubmissions { get; set; } = new();
    public List<TermSubmission> TermSubmissions { get; set; } = new();
}

public class MedicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string ProcedureName { get; set; } = string.Empty;
    public string TreatedArea { get; set; } = string.Empty; // ex: Terço superior da face, Lábios
    public string ParametersUsed { get; set; } = string.Empty; // ex: 15 Joules, Ácido Hialurônico 1ml, Agulha 30G
    public string ClinicalNotes { get; set; } = string.Empty; // Reação da pele, evolução
    public string PostCareInstructions { get; set; } = string.Empty; // Recomendações passadas
    public DateTime SessionDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProcedureType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPublicWebsite { get; set; } = true;
    public bool IsPriceHiddenOnWebsite { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeaturedInCarousel { get; set; } = true;
    public int? RecommendedReturnDays { get; set; }
}

public class Appointment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid ProcedureTypeId { get; set; }
    public string ProcedureName { get; set; } = string.Empty;
    public DateTime ScheduledDateTime { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public decimal TotalPrice { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Pix;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string ProfessionalName { get; set; } = "Dra. Mariana Siqueira";
    public string? Notes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    // Identificador técnico do evento criado pelo Painel no Google. Eventos externos nunca são persistidos aqui.
    public string? GoogleCalendarEventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GoogleCalendarConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public string? CalendarId { get; set; }
    public string? CalendarName { get; set; }
    public DateTime ConnectedAtUtc { get; set; }
    public DateTime? LastSuccessfulSyncAtUtc { get; set; }
}

public class ClinicOperationalSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OperatingHoursJson { get; set; } = "[]";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class FinancialEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FinancialEntryType Type { get; set; }
    public FinancialEntryStatus Status { get; set; } = FinancialEntryStatus.Settled;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Pix;
    public DateOnly EffectiveDate { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAtUtc { get; set; }
}

public class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InterestedProcedure { get; set; } = string.Empty;
    public LeadSource Source { get; set; } = LeadSource.Instagram;
    public string Status { get; set; } = "Novo";
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CmsContent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HeroTitle { get; set; } = "Realce Sua Beleza Natural com Ciência e Arte";
    public string HeroSubtitle { get; set; } = "Tratamentos estéticos avançados com atendimento exclusivo e personalizado para renovar sua autoestima.";
    public string DoctorName { get; set; } = "Dra. Mariana Siqueira";
    public string DoctorTitle { get; set; } = "Especialista em Harmonização & Cosmiatria";
    public string DoctorBio { get; set; } = "Com mais de 8 anos de excelência, dedicados a realçar contornos faciais e rejuvenescer com resultados naturais e seguros.";
    public string DoctorPhotoUrl { get; set; } = "/brand/dra-leilaine-portrait.jpg";
    public string LogoOnDarkUrl { get; set; } = "/brand/leilaine-arakaki-logo-light.png";
    public string LogoOnLightUrl { get; set; } = "/brand/leilaine-arakaki-logo-dark.png";
    public string WhatsappNumber { get; set; } = "5511999998888";
    public string AddressText { get; set; } = "Alameda Santos, 1200 - Jardins, São Paulo/SP";
    // Divulgação pública: independente do expediente usado pela agenda interna.
    public string PublicHoursWeekdays { get; set; } = "Segunda a Sexta: 08h00 às 19h00";
    public string PublicHoursSaturday { get; set; } = "Sábados: 08h00 às 13h00";
    public string PublicHoursNote { get; set; } = string.Empty;
    public string AboutTitle { get; set; } = string.Empty;
    public string AboutText { get; set; } = string.Empty;
    public string AboutImageUrl { get; set; } = string.Empty;
    public string ClinicTitle { get; set; } = string.Empty;
    public string ClinicText { get; set; } = string.Empty;
    public string ClinicImageUrl { get; set; } = string.Empty;
    public string CarouselItemsJson { get; set; } = "[]";
}

public class ClientPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public long OriginalFileSizeBytes { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public PhotoType Type { get; set; } = PhotoType.Before;
    public string ProcedureName { get; set; } = string.Empty;
    public bool IsPublicForWebsite { get; set; } = false;
    public bool ConsentGiven { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ClientDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "Termo de Consentimento";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class FormTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FormTemplateStatus Status { get; set; } = FormTemplateStatus.Draft;
    public string DraftSchemaJson { get; set; } = "{\"fields\":[]}";
    public int DraftRevision { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAtUtc { get; set; }
    public List<FormVersion> Versions { get; set; } = new();
}

public class FormVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormTemplateId { get; set; }
    public FormTemplate Template { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{\"fields\":[]}";
    public string SchemaHash { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public Guid PublishedByUserId { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public List<FormSubmission> Submissions { get; set; } = new();
}

public class FormSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public Guid FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;
    public FormSubmissionStatus Status { get; set; } = FormSubmissionStatus.Draft;
    public string AnswersJson { get; set; } = "{}";
    public string AnswersHash { get; set; } = string.Empty;
    // Artefato imutável produzido quando o preenchimento é finalizado.
    // Adendos preservam o histórico no banco, sem sobrescrever este PDF original.
    public string? FinalPdfPath { get; set; }
    public long? FinalPdfFileSizeBytes { get; set; }
    public string? FinalPdfSha256 { get; set; }
    public int Revision { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinalizedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public List<FormSubmissionAmendment> Amendments { get; set; } = new();
    public List<FormSignature> Signatures { get; set; } = new();
}

public class FormSubmissionAmendment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormSubmissionId { get; set; }
    public FormSubmission Submission { get; set; } = null!;
    public int AmendmentNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "{}";
    public string AnswersHash { get; set; } = string.Empty;
    public string PreviousAnswersHash { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class FormSignature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormSubmissionId { get; set; }
    public FormSubmission Submission { get; set; } = null!;
    public Guid? FormSubmissionAmendmentId { get; set; }
    public FormSubmissionAmendment? Amendment { get; set; }
    public string FieldId { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public string SignerDeclaration { get; set; } = string.Empty;
    public FormSignatureMethod Method { get; set; } = FormSignatureMethod.InPersonDrawn;
    public string StrokesJson { get; set; } = "[]";
    public string RenderedSvg { get; set; } = string.Empty;
    public string SignatureHash { get; set; } = string.Empty;
    public string AnswersHash { get; set; } = string.Empty;
    public string SchemaHash { get; set; } = string.Empty;
    public string PointerType { get; set; } = string.Empty;
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public Guid ConductedByUserId { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}

public class TermTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TermTemplateStatus Status { get; set; } = TermTemplateStatus.Draft;
    public string? DraftPdfPath { get; set; }
    public string? DraftPdfFileName { get; set; }
    public long DraftPdfFileSizeBytes { get; set; }
    public string? DraftPdfSha256 { get; set; }
    public string DraftLayoutJson { get; set; } = "{\"fields\":[]}";
    public int DraftRevision { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAtUtc { get; set; }
    public List<TermVersion> Versions { get; set; } = new();
}

public class TermVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TermTemplateId { get; set; }
    public TermTemplate Template { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PdfPath { get; set; } = string.Empty;
    public string PdfFileName { get; set; } = string.Empty;
    public long PdfFileSizeBytes { get; set; }
    public string PdfSha256 { get; set; } = string.Empty;
    public string LayoutJson { get; set; } = "{\"fields\":[]}";
    public string LayoutHash { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public Guid PublishedByUserId { get; set; }
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
    public List<TermSubmission> Submissions { get; set; } = new();
}

public class TermSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public Guid TermVersionId { get; set; }
    public TermVersion TermVersion { get; set; } = null!;
    public TermSubmissionStatus Status { get; set; } = TermSubmissionStatus.Draft;
    public string ValuesJson { get; set; } = "{\"values\":{},\"ink\":[]}";
    public string ValuesHash { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public string? FinalPdfPath { get; set; }
    public long? FinalPdfFileSizeBytes { get; set; }
    public string? FinalPdfSha256 { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinalizedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
}

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public string ActorUsername { get; set; } = "anonymous";
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Outcome { get; set; } = "Success";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
