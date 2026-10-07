# ADR-003: Modelagem Relacional PostgreSQL e Separação de Módulos

## Status
Parcialmente superado pelo ADR-008 para o prontuário clínico.

## Contexto
O sistema precisa lidar com dados cadastrais de clientes, agendamentos com horários e valores, ficha médica/estética de anamnese, arquivos de mídia (fotos e PDFs), CMS da Landing Page e captação de leads.

## Decisão
Utilizar o **PostgreSQL 16** com **Entity Framework Core 8** (Code-First) aplicando uma modelagem orientada a domínios com suporte nativo a Soft Delete.

### Entidades & Relacionamentos Principais:
1. **Módulo de Usuários & Auth**:
   - `Users`: `Id`, `Username`, `Email`, `PasswordHash`, `Role` (Admin, Secretária, Profissional), `IsActive`.
2. **Módulo de Clientes & Prontuário**:
   - `Clients`: Dados pessoais, CPF, `LeadSource` (Origem: Instagram, Indicação, etc.), `BirthDate`, `Address`, `Notes`.
   - `FormTemplates`, `FormTemplateVersions`, `FormSubmissions` e `FormSignatures`: formulários clínicos versionados para anamneses, consentimentos e questionários. A anamnese fixa foi removida pelo ADR-008.
   - `ClientDocuments`: `ClientId` (FK), `FileName`, `FilePath`, `DocumentType`, `UploadedAt`.
   - `ClientPhotos`: `ClientId` (FK), `ProcedureTypeId` (FK opcional), `FilePath`, `PhotoType` (Antes, Durante, Depois), `IsPublicForWebsite`, `ConsentGiven`.
3. **Módulo de Serviços & Agendamentos**:
   - `ProcedureTypes`: `Name`, `Description`, `Price`, `DurationMinutes`, `ImageUrl`, `IsPublicWebsite`, `IsActive`.
   - `Appointments`: `ClientId` (FK), `ProcedureTypeId` (FK), `ScheduledDateTime`, `CompletedDateTime`, `Status` (Scheduled, Confirmed, InProgress, Completed, Cancelled, NoShow), `TotalPrice`, `Notes`.
   - `AppointmentPayments`: `AppointmentId` (FK), `AmountPaid`, `PaymentMethod` (Pix, CreditCard, DebitCard, Cash), `PaymentStatus` (Paid, Partial, Pending), `PaymentDate`.
4. **Módulo de Leads & CMS**:
   - `Leads`: `Name`, `Email`, `Phone`, `InterestedProcedure`, `LeadSource`, `Status` (New, Contacted, Scheduled, Converted, Lost).
   - `CmsContents`: `SectionKey`, `Title`, `Subtitle`, `ContentJson`, `IsPublished`.

## Consequências
### Positivas:
- Integridade referencial forte em PostgreSQL.
- Histórico completo do financeiro e do ciclo de vida do cliente.
- Soft Delete global evita perda acidental de dados de prontuário.
