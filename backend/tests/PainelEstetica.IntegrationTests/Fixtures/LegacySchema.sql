CREATE TABLE "Users" (
    "Id" uuid PRIMARY KEY,
    "Username" text NOT NULL,
    "Email" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "Role" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE "Clients" (
    "Id" uuid PRIMARY KEY,
    "Name" text NOT NULL,
    "Email" text NOT NULL,
    "Phone" text NOT NULL,
    "Cpf" text NOT NULL,
    "Rg" text NOT NULL,
    "BirthDate" timestamp with time zone NULL,
    "Profession" text NOT NULL,
    "Source" integer NOT NULL,
    "Address" text NULL,
    "City" text NULL,
    "State" text NULL,
    "Notes" text NULL,
    "IsActive" boolean NOT NULL,
    "IsDeleted" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE "ProcedureTypes" (
    "Id" uuid PRIMARY KEY,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "Price" numeric NOT NULL,
    "DurationMinutes" integer NOT NULL,
    "ImageUrl" text NOT NULL,
    "IsPublicWebsite" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "IsFeaturedInCarousel" boolean NOT NULL
);

CREATE TABLE "Appointments" (
    "Id" uuid PRIMARY KEY,
    "ClientId" uuid NOT NULL REFERENCES "Clients" ("Id") ON DELETE RESTRICT,
    "ClientName" text NOT NULL,
    "ProcedureTypeId" uuid NOT NULL,
    "ProcedureName" text NOT NULL,
    "ScheduledDateTime" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "TotalPrice" numeric NOT NULL,
    "AmountPaid" numeric NOT NULL,
    "PaymentMethod" integer NOT NULL,
    "PaymentStatus" integer NOT NULL,
    "ProfessionalName" text NOT NULL,
    "Notes" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE "MedicalRecords" (
    "Id" uuid PRIMARY KEY,
    "ClientId" uuid NOT NULL REFERENCES "Clients" ("Id") ON DELETE CASCADE,
    "AppointmentId" uuid NULL,
    "ProcedureName" text NOT NULL,
    "TreatedArea" text NOT NULL,
    "ParametersUsed" text NOT NULL,
    "ClinicalNotes" text NOT NULL,
    "PostCareInstructions" text NOT NULL,
    "SessionDate" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE "Anamneses" (
    "Id" uuid PRIMARY KEY,
    "ClientId" uuid NOT NULL UNIQUE REFERENCES "Clients" ("Id") ON DELETE CASCADE,
    "MedicalHistory" text NOT NULL,
    "Allergies" text NOT NULL,
    "CurrentMedications" text NOT NULL,
    "SkinType" text NOT NULL,
    "HasBotoxOrFillers" boolean NOT NULL,
    "IsPregnantOrLactating" boolean NOT NULL,
    "AgreedToTerms" boolean NOT NULL,
    "SignedAt" timestamp with time zone NULL,
    "SignatureUrl" text NULL
);

CREATE TABLE "Leads" (
    "Id" uuid PRIMARY KEY,
    "Name" text NOT NULL,
    "Phone" text NOT NULL,
    "Email" text NOT NULL,
    "InterestedProcedure" text NOT NULL,
    "Source" integer NOT NULL,
    "Status" text NOT NULL,
    "Message" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE "CmsContents" (
    "Id" uuid PRIMARY KEY,
    "HeroTitle" text NOT NULL,
    "HeroSubtitle" text NOT NULL,
    "DoctorName" text NOT NULL,
    "DoctorTitle" text NOT NULL,
    "DoctorBio" text NOT NULL,
    "DoctorPhotoUrl" text NOT NULL,
    "WhatsappNumber" text NOT NULL,
    "AddressText" text NOT NULL
);

CREATE TABLE "ClientPhotos" (
    "Id" uuid PRIMARY KEY,
    "ClientId" uuid NOT NULL REFERENCES "Clients" ("Id") ON DELETE CASCADE,
    "FilePath" text NOT NULL,
    "Type" integer NOT NULL,
    "ProcedureName" text NOT NULL,
    "IsPublicForWebsite" boolean NOT NULL,
    "ConsentGiven" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);

CREATE TABLE "ClientDocuments" (
    "Id" uuid PRIMARY KEY,
    "ClientId" uuid NOT NULL REFERENCES "Clients" ("Id") ON DELETE CASCADE,
    "FileName" text NOT NULL,
    "FilePath" text NOT NULL,
    "DocumentType" text NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL
);

INSERT INTO "Users" VALUES (
    'aaaaaaaa-1111-1111-1111-111111111111',
    'legacy-admin',
    'legacy-admin@example.invalid',
    'legacy-hash-preserved',
    'Admin',
    TRUE,
    '2026-01-01T00:00:00Z'
);

INSERT INTO "Clients" VALUES (
    'bbbbbbbb-1111-1111-1111-111111111111',
    'Cliente legado preservado',
    'cliente@example.invalid',
    '67999999999',
    '12345678901',
    '',
    '1990-05-20T12:34:00Z',
    '',
    0,
    NULL,
    'Campo Grande',
    'MS',
    NULL,
    TRUE,
    FALSE,
    '2026-01-01T00:00:00Z'
);

INSERT INTO "ProcedureTypes" VALUES (
    'cccccccc-1111-1111-1111-111111111111',
    'Procedimento legado',
    'Registro usado para validar a atualização do schema.',
    100,
    30,
    '',
    TRUE,
    TRUE,
    FALSE
);
