using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PainelEstetica.Domain.Entities;
using PainelEstetica.Infrastructure.Identity;

namespace PainelEstetica.Infrastructure.Data;

public sealed class EsteticaDbContext(
    DbContextOptions<EsteticaDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<ProcedureType> ProcedureTypes => Set<ProcedureType>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<GoogleCalendarConnection> GoogleCalendarConnections => Set<GoogleCalendarConnection>();
    public DbSet<ClinicOperationalSettings> ClinicOperationalSettings => Set<ClinicOperationalSettings>();
    public DbSet<FinancialEntry> FinancialEntries => Set<FinancialEntry>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<CmsContent> CmsContents => Set<CmsContent>();
    public DbSet<ClientPhoto> ClientPhotos => Set<ClientPhoto>();
    public DbSet<ClientDocument> ClientDocuments => Set<ClientDocument>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormVersion> FormVersions => Set<FormVersion>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
    public DbSet<FormSubmissionAmendment> FormSubmissionAmendments => Set<FormSubmissionAmendment>();
    public DbSet<FormSignature> FormSignatures => Set<FormSignature>();
    public DbSet<TermTemplate> TermTemplates => Set<TermTemplate>();
    public DbSet<TermVersion> TermVersions => Set<TermVersion>();
    public DbSet<TermSubmission> TermSubmissions => Set<TermSubmission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentity(modelBuilder);
        ConfigureClients(modelBuilder);
        ConfigureForms(modelBuilder);
        ConfigureTerms(modelBuilder);
        ConfigureOperations(modelBuilder);
        ConfigureIntegrations(modelBuilder);
        ConfigurePublicContent(modelBuilder);
        ConfigureAudit(modelBuilder);
        SeedPublicContent(modelBuilder);
    }

    private static void ConfigureForms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormTemplate>(entity =>
        {
            entity.Property(template => template.Name).HasMaxLength(160);
            entity.Property(template => template.Category).HasMaxLength(100);
            entity.Property(template => template.Description).HasMaxLength(1000);
            entity.Property(template => template.DraftSchemaJson).HasColumnType("jsonb");
            entity.Property(template => template.DraftRevision).IsConcurrencyToken();
            entity.HasIndex(template => new { template.Status, template.UpdatedAtUtc });
            entity.HasIndex(template => template.Name);
        });

        modelBuilder.Entity<FormVersion>(entity =>
        {
            entity.Property(version => version.Name).HasMaxLength(160);
            entity.Property(version => version.Category).HasMaxLength(100);
            entity.Property(version => version.Description).HasMaxLength(1000);
            entity.Property(version => version.SchemaJson).HasColumnType("jsonb");
            entity.Property(version => version.SchemaHash).HasMaxLength(64).IsFixedLength();
            entity.Property(version => version.ChangeSummary).HasMaxLength(500);
            entity.HasIndex(version => new { version.FormTemplateId, version.VersionNumber }).IsUnique();
            entity.HasOne(version => version.Template)
                .WithMany(template => template.Versions)
                .HasForeignKey(version => version.FormTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormSubmission>(entity =>
        {
            entity.HasQueryFilter(item => !item.Client.IsDeleted);
            entity.Property(item => item.AnswersJson).HasColumnType("jsonb");
            entity.Property(item => item.AnswersHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.FinalPdfPath).HasMaxLength(255);
            entity.Property(item => item.FinalPdfSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.Property(item => item.VoidReason).HasMaxLength(1000);
            entity.HasIndex(item => new { item.ClientId, item.UpdatedAtUtc });
            entity.HasIndex(item => new { item.AppointmentId, item.UpdatedAtUtc });
            entity.HasIndex(item => new { item.FormVersionId, item.Status });
            entity.HasOne(item => item.Client)
                .WithMany(client => client.FormSubmissions)
                .HasForeignKey(item => item.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Appointment)
                .WithMany()
                .HasForeignKey(item => item.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.FormVersion)
                .WithMany(version => version.Submissions)
                .HasForeignKey(item => item.FormVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FormSubmissionAmendment>(entity =>
        {
            entity.HasQueryFilter(item => !item.Submission.Client.IsDeleted);
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.Property(item => item.AnswersJson).HasColumnType("jsonb");
            entity.Property(item => item.AnswersHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.PreviousAnswersHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(item => new { item.FormSubmissionId, item.AmendmentNumber }).IsUnique();
            entity.HasOne(item => item.Submission)
                .WithMany(submission => submission.Amendments)
                .HasForeignKey(item => item.FormSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormSignature>(entity =>
        {
            entity.HasQueryFilter(item => !item.Submission.Client.IsDeleted);
            entity.Property(item => item.FieldId).HasMaxLength(64);
            entity.Property(item => item.SignerName).HasMaxLength(160);
            entity.Property(item => item.SignerDeclaration).HasMaxLength(1000);
            entity.Property(item => item.StrokesJson).HasColumnType("jsonb");
            entity.Property(item => item.RenderedSvg).HasColumnType("text");
            entity.Property(item => item.SignatureHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.AnswersHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.SchemaHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.PointerType).HasMaxLength(20);
            entity.HasIndex(item => new { item.FormSubmissionId, item.FieldId });
            entity.HasOne(item => item.Submission)
                .WithMany(submission => submission.Signatures)
                .HasForeignKey(item => item.FormSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Amendment)
                .WithMany()
                .HasForeignKey(item => item.FormSubmissionAmendmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTerms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TermTemplate>(entity =>
        {
            entity.Property(template => template.Name).HasMaxLength(160);
            entity.Property(template => template.Description).HasMaxLength(1000);
            entity.Property(template => template.DraftPdfPath).HasMaxLength(255);
            entity.Property(template => template.DraftPdfFileName).HasMaxLength(255);
            entity.Property(template => template.DraftPdfSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(template => template.DraftLayoutJson).HasColumnType("jsonb");
            entity.Property(template => template.DraftRevision).IsConcurrencyToken();
            entity.HasIndex(template => new { template.Status, template.UpdatedAtUtc });
            entity.HasIndex(template => template.Name);
        });

        modelBuilder.Entity<TermVersion>(entity =>
        {
            entity.Property(version => version.Name).HasMaxLength(160);
            entity.Property(version => version.Description).HasMaxLength(1000);
            entity.Property(version => version.PdfPath).HasMaxLength(255);
            entity.Property(version => version.PdfFileName).HasMaxLength(255);
            entity.Property(version => version.PdfSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(version => version.LayoutJson).HasColumnType("jsonb");
            entity.Property(version => version.LayoutHash).HasMaxLength(64).IsFixedLength();
            entity.Property(version => version.ChangeSummary).HasMaxLength(500);
            entity.HasIndex(version => new { version.TermTemplateId, version.VersionNumber }).IsUnique();
            entity.HasOne(version => version.Template)
                .WithMany(template => template.Versions)
                .HasForeignKey(version => version.TermTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TermSubmission>(entity =>
        {
            entity.HasQueryFilter(item => !item.Client.IsDeleted);
            entity.Property(item => item.ValuesJson).HasColumnType("jsonb");
            entity.Property(item => item.ValuesHash).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.Property(item => item.FinalPdfPath).HasMaxLength(255);
            entity.Property(item => item.FinalPdfSha256).HasMaxLength(64).IsFixedLength();
            entity.Property(item => item.VoidReason).HasMaxLength(1000);
            entity.HasIndex(item => new { item.ClientId, item.UpdatedAtUtc });
            entity.HasIndex(item => new { item.AppointmentId, item.UpdatedAtUtc });
            entity.HasIndex(item => new { item.TermVersionId, item.Status });
            entity.HasOne(item => item.Client)
                .WithMany(client => client.TermSubmissions)
                .HasForeignKey(item => item.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Appointment)
                .WithMany()
                .HasForeignKey(item => item.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(item => item.TermVersion)
                .WithMany(version => version.Submissions)
                .HasForeignKey(item => item.TermVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.UserName).HasColumnName("Username").HasMaxLength(80);
            entity.Property(user => user.NormalizedUserName).HasMaxLength(80);
            entity.Property(user => user.Email).HasMaxLength(254);
            entity.Property(user => user.NormalizedEmail).HasMaxLength(254);
            entity.Property(user => user.CreatedAtUtc).HasColumnName("CreatedAt");
            entity.HasIndex(user => user.NormalizedUserName).IsUnique();
            entity.HasIndex(user => user.NormalizedEmail);
        });

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.ToTable("UserPermissionOverrides");
            entity.HasKey(permission => new { permission.UserId, permission.Permission });
            entity.Property(permission => permission.Permission).HasMaxLength(100);
            entity.HasOne(permission => permission.User)
                .WithMany(user => user.PermissionOverrides)
                .HasForeignKey(permission => permission.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureClients(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.Property(client => client.Name).HasMaxLength(160);
            entity.Property(client => client.Email).HasMaxLength(254);
            entity.Property(client => client.Phone).HasMaxLength(30);
            entity.Property(client => client.Cpf).HasMaxLength(14);
            entity.Property(client => client.Rg).HasMaxLength(30);
            entity.Property(client => client.Profession).HasMaxLength(120);
            entity.Property(client => client.Address).HasMaxLength(240);
            entity.Property(client => client.City).HasMaxLength(120);
            entity.Property(client => client.State).HasMaxLength(2);
            entity.Property(client => client.Notes).HasMaxLength(2000);
            entity.HasIndex(client => client.Name);
            entity.HasIndex(client => client.Phone);
            entity.HasIndex(client => client.Cpf)
                .IsUnique()
                .HasFilter("\"Cpf\" <> '' AND NOT \"IsDeleted\"");
            entity.HasQueryFilter(client => !client.IsDeleted);
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.Property(record => record.ProcedureName).HasMaxLength(160);
            entity.Property(record => record.TreatedArea).HasMaxLength(1000);
            entity.Property(record => record.ParametersUsed).HasMaxLength(2000);
            entity.Property(record => record.ClinicalNotes).HasMaxLength(4000);
            entity.Property(record => record.PostCareInstructions).HasMaxLength(4000);
            entity.HasIndex(record => new { record.ClientId, record.SessionDate });
            entity.HasOne<Client>()
                .WithMany(client => client.MedicalRecords)
                .HasForeignKey(record => record.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Appointment>()
                .WithMany()
                .HasForeignKey(record => record.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ClientPhoto>(entity =>
        {
            entity.Property(photo => photo.OriginalFileName).HasMaxLength(255);
            entity.Property(photo => photo.FilePath).HasMaxLength(500);
            entity.Property(photo => photo.ContentType).HasMaxLength(100);
            entity.Property(photo => photo.Sha256).HasMaxLength(64).IsFixedLength();
            entity.Property(photo => photo.ProcedureName).HasMaxLength(160);
            entity.HasIndex(photo => new { photo.ClientId, photo.CreatedAt });
            entity.HasOne<Client>()
                .WithMany(client => client.Photos)
                .HasForeignKey(photo => photo.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientDocument>(entity =>
        {
            entity.Property(document => document.FileName).HasMaxLength(255);
            entity.Property(document => document.FilePath).HasMaxLength(500);
            entity.Property(document => document.ContentType).HasMaxLength(150);
            entity.Property(document => document.Sha256).HasMaxLength(64).IsFixedLength();
            entity.Property(document => document.DocumentType).HasMaxLength(120);
            entity.HasIndex(document => new { document.ClientId, document.UploadedAt });
            entity.HasOne<Client>()
                .WithMany(client => client.Documents)
                .HasForeignKey(document => document.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(appointment => appointment.ClientName).HasMaxLength(160);
            entity.Property(appointment => appointment.ProcedureName).HasMaxLength(160);
            entity.Property(appointment => appointment.ProfessionalName).HasMaxLength(160);
            entity.Property(appointment => appointment.Notes).HasMaxLength(2000);
            entity.Property(appointment => appointment.GoogleCalendarEventId).HasMaxLength(1024);
            entity.Property(appointment => appointment.TotalPrice).HasPrecision(12, 2);
            entity.Property(appointment => appointment.AmountPaid).HasPrecision(12, 2);
            entity.HasIndex(appointment => appointment.ScheduledDateTime);
            entity.HasIndex(appointment => new { appointment.ClientId, appointment.ScheduledDateTime });
            entity.HasIndex(appointment => new { appointment.Status, appointment.CompletedAtUtc });
            entity.HasOne<Client>()
                .WithMany(client => client.Appointments)
                .HasForeignKey(appointment => appointment.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcedureType>()
                .WithMany()
                .HasForeignKey(appointment => appointment.ProcedureTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProcedureType>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ProcedureTypes_RecommendedReturnDays",
                "\"RecommendedReturnDays\" IS NULL OR \"RecommendedReturnDays\" BETWEEN 1 AND 3650"));
            entity.Property(procedure => procedure.Name).HasMaxLength(160);
            entity.Property(procedure => procedure.Description).HasMaxLength(2000);
            entity.Property(procedure => procedure.ImageUrl).HasMaxLength(500);
            entity.Property(procedure => procedure.Price).HasPrecision(12, 2);
            entity.HasIndex(procedure => procedure.Name).IsUnique();
        });

        modelBuilder.Entity<FinancialEntry>(entity =>
        {
            entity.Property(entry => entry.Category).HasMaxLength(100);
            entity.Property(entry => entry.Description).HasMaxLength(240);
            entity.Property(entry => entry.Amount).HasPrecision(12, 2);
            entity.Property(entry => entry.Notes).HasMaxLength(2000);
            entity.HasIndex(entry => new { entry.EffectiveDate, entry.Status });
            entity.HasIndex(entry => new { entry.Type, entry.EffectiveDate });
            entity.HasIndex(entry => entry.CreatedByUserId);
        });

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.Property(lead => lead.Name).HasMaxLength(160);
            entity.Property(lead => lead.Phone).HasMaxLength(30);
            entity.Property(lead => lead.Email).HasMaxLength(254);
            entity.Property(lead => lead.InterestedProcedure).HasMaxLength(160);
            entity.Property(lead => lead.Status).HasMaxLength(40);
            entity.Property(lead => lead.Message).HasMaxLength(2000);
            entity.HasIndex(lead => new { lead.Status, lead.CreatedAt });
            entity.HasIndex(lead => lead.Phone);
        });
    }

    private static void ConfigureIntegrations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GoogleCalendarConnection>(entity =>
        {
            entity.ToTable("GoogleCalendarConnections");
            entity.Property(connection => connection.EncryptedRefreshToken).HasMaxLength(4096);
            entity.Property(connection => connection.CalendarId).HasMaxLength(512);
            entity.Property(connection => connection.CalendarName).HasMaxLength(512);
            entity.HasIndex(connection => connection.CalendarId);
        });

        modelBuilder.Entity<ClinicOperationalSettings>(entity =>
        {
            entity.ToTable("ClinicOperationalSettings");
            entity.Property(settings => settings.OperatingHoursJson).HasColumnType("jsonb").HasDefaultValue("[]");
        });
    }

    private static void ConfigurePublicContent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CmsContent>(entity =>
        {
            entity.Property(content => content.HeroTitle).HasMaxLength(240);
            entity.Property(content => content.HeroSubtitle).HasMaxLength(500);
            entity.Property(content => content.DoctorName).HasMaxLength(160);
            entity.Property(content => content.DoctorTitle).HasMaxLength(200);
            entity.Property(content => content.DoctorBio).HasMaxLength(3000);
            entity.Property(content => content.DoctorPhotoUrl).HasMaxLength(500);
            entity.Property(content => content.LogoOnDarkUrl).HasMaxLength(500);
            entity.Property(content => content.LogoOnLightUrl).HasMaxLength(500);
            entity.Property(content => content.WhatsappNumber).HasMaxLength(30);
            entity.Property(content => content.AddressText).HasMaxLength(300);
            entity.Property(content => content.PublicHoursWeekdays).HasMaxLength(120);
            entity.Property(content => content.PublicHoursSaturday).HasMaxLength(120);
            entity.Property(content => content.PublicHoursNote).HasMaxLength(240);
            entity.Property(content => content.AboutTitle).HasMaxLength(160);
            entity.Property(content => content.AboutText).HasMaxLength(3000);
            entity.Property(content => content.AboutImageUrl).HasMaxLength(500);
            entity.Property(content => content.ClinicTitle).HasMaxLength(160);
            entity.Property(content => content.ClinicText).HasMaxLength(3000);
            entity.Property(content => content.ClinicImageUrl).HasMaxLength(500);
            entity.Property(content => content.CarouselItemsJson).HasColumnType("jsonb").HasDefaultValue("[]");
        });
    }

    private static void ConfigureAudit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.Property(audit => audit.ActorUsername).HasMaxLength(80);
            entity.Property(audit => audit.Action).HasMaxLength(120);
            entity.Property(audit => audit.ResourceType).HasMaxLength(100);
            entity.Property(audit => audit.ResourceId).HasMaxLength(120);
            entity.Property(audit => audit.Outcome).HasMaxLength(30);
            entity.Property(audit => audit.IpAddress).HasMaxLength(64);
            entity.Property(audit => audit.UserAgent).HasMaxLength(500);
            entity.Property(audit => audit.DetailsJson).HasMaxLength(4000);
            entity.HasIndex(audit => audit.OccurredAtUtc);
            entity.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAtUtc });
            entity.HasIndex(audit => new { audit.ResourceType, audit.ResourceId });
        });
    }

    private static void SeedPublicContent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CmsContent>().HasData(new CmsContent
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            HeroTitle = "Realce Sua Beleza Natural com Ciência e Elegância",
            HeroSubtitle = "Tratamentos estéticos faciais e corporais avançados com atendimento exclusivo e personalizado para renovar sua autoestima.",
            DoctorName = "Dra. Mariana Siqueira",
            DoctorTitle = "Dermatofuncional & Cosmiatria Avançada",
            DoctorBio = "Especialista renomada com mais de 8 anos transformando vidas, unindo protocolos de alta tecnologia e refinamento para resultados naturais.",
            DoctorPhotoUrl = "/brand/dra-leilaine-portrait.jpg",
            LogoOnDarkUrl = "/brand/leilaine-arakaki-logo-light.png",
            LogoOnLightUrl = "/brand/leilaine-arakaki-logo-dark.png",
            WhatsappNumber = "5511999998888",
            AddressText = "Alameda Santos, 1200 - Jardins, São Paulo/SP"
        });

        modelBuilder.Entity<ProcedureType>().HasData(
            new ProcedureType
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Harmonização Facial & Botox",
                Description = "Suavização de linhas de expressão e contorno facial harmônico com toxina botulínica e ácido hialurônico.",
                Price = 1200.00m,
                DurationMinutes = 45,
                ImageUrl = "https://images.unsplash.com/photo-1570172619644-dfd03ed5d881?q=80&w=800&auto=format&fit=crop",
                IsPublicWebsite = true,
                IsActive = true,
                IsFeaturedInCarousel = true
            },
            new ProcedureType
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "Bioestimulador de Colágeno",
                Description = "Estímulo natural da firmeza da pele facial e corporal, combatendo a flacidez com resultados duradouros.",
                Price = 1800.00m,
                DurationMinutes = 60,
                ImageUrl = "https://images.unsplash.com/photo-1616394584738-fc6e612e71b9?q=80&w=800&auto=format&fit=crop",
                IsPublicWebsite = true,
                IsActive = true,
                IsFeaturedInCarousel = true
            },
            new ProcedureType
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Limpeza de Pele Fotônica",
                Description = "Higienização profunda com extração indolor, peeling ultrassônico e fototerapia LED para viço e pureza.",
                Price = 350.00m,
                DurationMinutes = 75,
                ImageUrl = "https://images.unsplash.com/photo-1512290900673-7002fffe9353?q=80&w=800&auto=format&fit=crop",
                IsPublicWebsite = true,
                IsActive = true,
                IsFeaturedInCarousel = true
            });
    }
}
