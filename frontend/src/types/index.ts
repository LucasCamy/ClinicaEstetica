export type AppointmentStatus = 'Scheduled' | 'Confirmed' | 'InProgress' | 'Completed' | 'Cancelled' | 'NoShow';
export type PaymentMethod = 'Pix' | 'CreditCard' | 'DebitCard' | 'Cash' | 'PackageSession';
export type PaymentStatus = 'Paid' | 'Partial' | 'Pending';
export type FinancialEntryType = 'Income' | 'Expense';
export type FinancialEntryStatus = 'Planned' | 'Settled' | 'Cancelled';
export type FinancialLedgerSource = 'AppointmentReceipt' | 'ManualEntry';
export type TermTemplateStatus = 'Draft' | 'Published' | 'Archived';
export type TermFieldType = 'Text' | 'Date' | 'Checkbox' | 'Handwriting' | 'Signature';
export type TermSubmissionStatus = 'Draft' | 'Finalized' | 'Voided';
export type LeadSource = 'Instagram' | 'Indication' | 'GoogleAds' | 'WhatsApp' | 'WalkIn' | 'Other' | 'Website';
export type PhotoType = 'Before' | 'Progress' | 'After';

export interface User {
  id: string;
  username: string;
  email: string;
  roles: string[];
  permissions: string[];
  mustChangePassword: boolean;
  mfaEnabled: boolean;
  requiresMfaSetup: boolean;
}

export interface LoginResponse {
  status: 'Authenticated' | 'RequiresTwoFactor';
  user?: User;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface MfaSetup {
  sharedKey: string;
  authenticatorUri: string;
}

export interface MfaRecoveryCodes {
  recoveryCodes: string[];
}

export interface UserPermissionOverride {
  permission: string;
  isGranted: boolean;
}

export interface AdminUser {
  id: string;
  username: string;
  email: string;
  isActive: boolean;
  mustChangePassword: boolean;
  mfaEnabled: boolean;
  createdAtUtc: string;
  lastLoginAtUtc?: string;
  roles: string[];
  permissionOverrides: UserPermissionOverride[];
  effectivePermissions: string[];
}

export interface AccessCatalog {
  roles: string[];
  permissions: string[];
}

export interface MedicalRecord {
  id: string;
  clientId: string;
  appointmentId?: string;
  procedureName: string;
  treatedArea: string;
  parametersUsed: string;
  clinicalNotes: string;
  postCareInstructions: string;
  sessionDate: string;
  createdAt: string;
}

export interface ClientPhoto {
  id: string;
  clientId: string;
  filePath?: string;
  contentUrl?: string;
  type: PhotoType;
  procedureName: string;
  isPublicForWebsite: boolean;
  consentGiven: boolean;
  createdAt: string;
  originalFileSizeBytes: number;
  fileSizeBytes: number;
  width: number;
  height: number;
}

export interface ClientDocument {
  id: string;
  clientId: string;
  fileName: string;
  filePath?: string;
  contentUrl?: string;
  documentType: string;
  uploadedAt: string;
  contentType: string;
  fileSizeBytes: number;
}

export interface ClinicalStorageUsage {
  clientUsedBytes: number;
  clientLimitBytes: number;
  installationUsedBytes: number;
  installationLimitBytes: number;
}

export type FormTemplateStatus = 'Draft' | 'Published' | 'Archived';

export type FormFieldType =
  | 'Section'
  | 'InformationalText'
  | 'ShortText'
  | 'LongText'
  | 'Number'
  | 'Date'
  | 'YesNo'
  | 'Checkbox'
  | 'CheckboxGroup'
  | 'Dropdown'
  | 'MultiSelect'
  | 'Signature';

export interface FormFieldOption {
  id: string;
  label: string;
}

export interface FormFieldDefinition {
  id: string;
  type: FormFieldType;
  label: string;
  description: string;
  required: boolean;
  placeholder: string;
  options: FormFieldOption[];
}

export interface FormSchemaDefinition {
  fields: FormFieldDefinition[];
}

export interface FormTemplateSummary {
  id: string;
  name: string;
  category: string;
  description: string;
  status: FormTemplateStatus;
  draftRevision: number;
  draftFieldCount: number;
  latestPublishedVersionNumber?: number;
  hasUnpublishedChanges: boolean;
  updatedAtUtc: string;
}

export interface FormVersionSummary {
  id: string;
  versionNumber: number;
  fieldCount: number;
  changeSummary: string;
  publishedAtUtc: string;
  publishedByUserId: string;
}

export interface FormVersion {
  id: string;
  formTemplateId: string;
  versionNumber: number;
  name: string;
  category: string;
  description: string;
  schema: FormSchemaDefinition;
  schemaHash: string;
  changeSummary: string;
  publishedAtUtc: string;
  publishedByUserId: string;
}

export interface FormTemplateDetail {
  id: string;
  name: string;
  category: string;
  description: string;
  status: FormTemplateStatus;
  draftRevision: number;
  draftSchema: FormSchemaDefinition;
  hasUnpublishedChanges: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  versions: FormVersionSummary[];
}

export type FormSubmissionStatus = 'Draft' | 'Finalized' | 'Amended' | 'Voided';
export type FormAnswerValue = string | number | boolean | string[];
export type FormAnswers = Record<string, FormAnswerValue>;

export interface AvailableFormVersion {
  formTemplateId: string;
  formVersionId: string;
  versionNumber: number;
  name: string;
  category: string;
  description: string;
  schema: FormSchemaDefinition;
  schemaHash: string;
}

export interface SignaturePoint {
  x: number;
  y: number;
}

export interface FormSignatureCapture {
  fieldId: string;
  signerName: string;
  pointerType: 'touch' | 'pen' | 'mouse' | 'unknown';
  canvasWidth: number;
  canvasHeight: number;
  strokes: SignaturePoint[][];
}

export interface FormSignature {
  id: string;
  fieldId: string;
  signerName: string;
  signerDeclaration: string;
  method: 'InPersonDrawn';
  signatureHash: string;
  answersHash: string;
  schemaHash: string;
  pointerType: string;
  capturedAtUtc: string;
  conductedByUserId: string;
  amendmentId?: string;
  contentUrl: string;
}

export interface FormSubmissionAmendment {
  id: string;
  amendmentNumber: number;
  reason: string;
  answers: FormAnswers;
  answersHash: string;
  previousAnswersHash: string;
  createdByUserId: string;
  createdAtUtc: string;
}

export interface FormSubmissionSummary {
  id: string;
  clientId: string;
  appointmentId?: string;
  formVersionId: string;
  formName: string;
  versionNumber: number;
  status: FormSubmissionStatus;
  revision: number;
  isSigned: boolean;
  hasFinalPdf: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  finalizedAtUtc?: string;
}

export interface FormSubmission {
  id: string;
  clientId: string;
  appointmentId?: string;
  formVersionId: string;
  formTemplateId: string;
  formName: string;
  category: string;
  versionNumber: number;
  schemaHash: string;
  schema: FormSchemaDefinition;
  status: FormSubmissionStatus;
  revision: number;
  originalAnswers: FormAnswers;
  originalAnswersHash: string;
  finalPdfSha256?: string;
  finalPdfFileSizeBytes?: number;
  finalPdfContentUrl?: string;
  effectiveAnswers: FormAnswers;
  effectiveAnswersHash: string;
  createdByUserId: string;
  updatedByUserId: string;
  finalizedByUserId?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  finalizedAtUtc?: string;
  voidedByUserId?: string;
  voidedAtUtc?: string;
  voidReason?: string;
  signatures: FormSignature[];
  amendments: FormSubmissionAmendment[];
}

export interface TermFieldDefinition {
  id: string;
  type: TermFieldType;
  label: string;
  required: boolean;
  placeholder: string;
  pageNumber: number;
  x: number;
  y: number;
  width: number;
  height: number;
  multiline: boolean;
}

export interface TermLayoutDefinition {
  fields: TermFieldDefinition[];
}

export interface TermTemplateSummary {
  id: string;
  name: string;
  description: string;
  status: TermTemplateStatus;
  draftRevision: number;
  hasDraftPdf: boolean;
  draftFieldCount: number;
  latestPublishedVersionNumber?: number;
  hasUnpublishedChanges: boolean;
  updatedAtUtc: string;
}

export interface TermVersionSummary {
  id: string;
  versionNumber: number;
  fieldCount: number;
  changeSummary: string;
  publishedAtUtc: string;
  publishedByUserId: string;
  pdfFileSizeBytes: number;
  pdfSha256: string;
}

export interface TermTemplateDetail {
  id: string;
  name: string;
  description: string;
  status: TermTemplateStatus;
  draftRevision: number;
  hasDraftPdf: boolean;
  draftPdfFileName?: string;
  draftPdfFileSizeBytes: number;
  draftPdfSha256?: string;
  draftLayout: TermLayoutDefinition;
  hasUnpublishedChanges: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  versions: TermVersionSummary[];
}

export interface TermVersion {
  id: string;
  termTemplateId: string;
  versionNumber: number;
  name: string;
  description: string;
  pdfFileName: string;
  pdfFileSizeBytes: number;
  pdfSha256: string;
  layout: TermLayoutDefinition;
  layoutHash: string;
  changeSummary: string;
  publishedAtUtc: string;
  publishedByUserId: string;
}

export interface AvailableTermVersion {
  termTemplateId: string;
  termVersionId: string;
  versionNumber: number;
  name: string;
  description: string;
  pdfSha256: string;
  layout: TermLayoutDefinition;
  layoutHash: string;
}

export interface TermInkPoint {
  x: number;
  y: number;
}

export interface TermInkCapture {
  fieldId: string;
  signerName?: string;
  pointerType: 'touch' | 'pen' | 'mouse' | 'unknown';
  declarationAccepted?: boolean;
  strokes: TermInkPoint[][];
}

export interface TermSubmissionPayload {
  values: Record<string, string | boolean | null>;
  ink: TermInkCapture[];
}

export interface TermSubmissionSummary {
  id: string;
  clientId: string;
  appointmentId?: string;
  termVersionId: string;
  termName: string;
  versionNumber: number;
  status: TermSubmissionStatus;
  revision: number;
  isSigned: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  finalizedAtUtc?: string;
}

export interface TermSubmission {
  id: string;
  clientId: string;
  appointmentId?: string;
  termVersionId: string;
  termTemplateId: string;
  termName: string;
  versionNumber: number;
  pdfSha256: string;
  layout: TermLayoutDefinition;
  layoutHash: string;
  status: TermSubmissionStatus;
  revision: number;
  payload: TermSubmissionPayload;
  valuesHash: string;
  finalPdfSha256?: string;
  finalPdfFileSizeBytes?: number;
  createdByUserId: string;
  updatedByUserId: string;
  finalizedByUserId?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  finalizedAtUtc?: string;
  voidedByUserId?: string;
  voidedAtUtc?: string;
  voidReason?: string;
  finalPdfContentUrl?: string;
}

export interface Client {
  id: string;
  name: string;
  email: string;
  phone: string;
  cpf: string;
  rg?: string;
  birthDate?: string;
  profession?: string;
  source: LeadSource;
  address?: string;
  city?: string;
  state?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  medicalRecords?: MedicalRecord[];
  appointments?: Appointment[];
  photos?: ClientPhoto[];
  documents?: ClientDocument[];
}

export interface ProcedureCount {
  procedureName: string;
  count: number;
}

export interface ClientSummary {
  clientId: string;
  totalAppointments: number;
  completedAppointments: number;
  upcomingAppointments: number;
  nextAppointmentAt?: string;
  clinicalRecordsCount: number;
  lastClinicalRecordAt?: string;
  mostPerformedProcedures: ProcedureCount[];
  documentsCount: number;
  photosCount: number;
  draftFormsCount: number;
  finalizedFormsCount: number;
  signedFormsCount: number;
  totalContracted: number;
  totalPaid: number;
  outstandingBalance: number;
}

export interface ProcedureType {
  id: string;
  name: string;
  description: string;
  price: number;
  durationMinutes: number;
  imageUrl: string;
  isPublicWebsite: boolean;
  isPriceHiddenOnWebsite: boolean;
  isActive: boolean;
  isFeaturedInCarousel: boolean;
  recommendedReturnDays?: number | null;
}

export interface Appointment {
  id: string;
  clientId: string;
  clientName: string;
  procedureTypeId: string;
  procedureName: string;
  scheduledDateTime: string;
  status: AppointmentStatus;
  totalPrice: number;
  amountPaid: number;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  professionalName?: string;
  notes?: string;
  completedAtUtc?: string | null;
  createdAt: string;
  googleCalendarSyncWarning?: string | null;
}

export interface GoogleCalendarConnection {
  isConfigured: boolean;
  isConnected: boolean;
  hasSelectedCalendar: boolean;
  calendarId?: string | null;
  calendarName?: string | null;
  connectedAtUtc?: string | null;
  lastSuccessfulSyncAtUtc?: string | null;
  setupMessage?: string | null;
}

export interface GoogleCalendarListItem {
  id: string;
  name: string;
  isPrimary: boolean;
  accessRole: string;
  color?: string | null;
}

export interface GoogleCalendarEvent {
  id: string;
  title: string;
  startUtc: string;
  endUtc: string;
  isAllDay: boolean;
  htmlLink?: string | null;
  color?: string | null;
}

export interface GoogleCalendarEventsResult {
  isConnected: boolean;
  hasSelectedCalendar: boolean;
  events: GoogleCalendarEvent[];
  notice?: string | null;
}

export interface OperatingHoursDay {
  dayOfWeek: number;
  isOpen: boolean;
  startTime: string;
  endTime: string;
}

export interface ClinicOperationalSettings {
  timeZoneId: string;
  operatingHours: OperatingHoursDay[];
  updatedAtUtc: string;
}

export interface StorageUsage {
  usedBytes: number;
  limitBytes: number;
}

export interface Lead {
  id: string;
  name: string;
  phone: string;
  email: string;
  interestedProcedure: string;
  source: LeadSource;
  status: string;
  message?: string;
  website?: string;
  createdAt: string;
}

export interface CmsContent {
  id?: string;
  heroTitle: string;
  heroSubtitle: string;
  doctorName: string;
  doctorTitle: string;
  doctorBio: string;
  doctorPhotoUrl: string;
  logoOnDarkUrl: string;
  logoOnLightUrl: string;
  whatsappNumber: string;
  addressText: string;
  publicHoursWeekdays: string;
  publicHoursSaturday: string;
  publicHoursNote: string;
  aboutTitle: string;
  aboutText: string;
  aboutImageUrl: string;
  clinicTitle: string;
  clinicText: string;
  clinicImageUrl: string;
  carouselItems: LandingCarouselItem[];
}

export interface LandingCarouselItem {
  id: string;
  imageUrl: string;
  title: string;
  description: string;
}

export interface CmsImageUpload {
  url: string;
}

export interface DashboardStats {
  totalClients: number;
  todayAppointments: number;
  monthlyRevenue: number;
  pendingLeads: number;
  activeProcedures: number;
  publishedForms: number;
  finalizedFormsThisMonth: number;
  publishedTerms: number;
  finalizedTermsThisMonth: number;
  monthlyExpenses: number;
  appointmentsThisMonth: number;
  scheduledAppointmentsThisMonth: number;
  confirmedAppointmentsThisMonth: number;
  inProgressAppointmentsThisMonth: number;
  completedAppointmentsThisMonth: number;
  cancelledAppointmentsThisMonth: number;
  noShowAppointmentsThisMonth: number;
  upcomingSchedule: DashboardScheduleDay[];
  upcomingBirthdays: DashboardBirthday[];
  topProcedures: DashboardProcedureRanking[];
  dueReturnsCount: number;
  dueReturns: DashboardReturnAlert[];
}

export interface DashboardScheduleDay {
  date: string;
  scheduled: number;
  confirmed: number;
  inProgress: number;
  completed: number;
  cancelled: number;
  noShow: number;
}

export interface DashboardBirthday {
  clientId: string;
  name: string;
  birthDate: string;
  turningAge: number;
  daysUntil: number;
}

export interface DashboardProcedureRanking {
  procedureName: string;
  appointments: number;
  amountReceived: number;
}

export interface DashboardReturnAlert {
  clientId: string;
  clientName: string;
  procedureName: string;
  lastCompletedDate: string;
  returnDueDate: string;
  daysUntil: number;
}

export type ReportAppointmentStatus = AppointmentStatus;
export type ReportPaymentStatus = PaymentStatus;

export interface CustomReportRequest {
  fields: string[];
  fromDate?: string;
  toDate?: string;
  procedureTypeId?: string;
  appointmentStatuses: ReportAppointmentStatus[];
  paymentStatuses: ReportPaymentStatus[];
  birthdayMonth?: number;
  search?: string;
  sortBy?: string;
  sortDescending: boolean;
  page: number;
  pageSize: number;
}

export interface CustomReportColumn {
  key: string;
  label: string;
}

export interface CustomReportRow {
  appointmentId: string;
  clientId: string;
  values: Record<string, string | null>;
}

export interface CustomReportResult {
  columns: CustomReportColumn[];
  rows: CustomReportRow[];
  totalCount: number;
}

export interface FinancialEntry {
  id: string;
  type: FinancialEntryType;
  status: FinancialEntryStatus;
  category: string;
  description: string;
  amount: number;
  paymentMethod: PaymentMethod;
  effectiveDate: string;
  notes?: string;
  createdAtUtc: string;
}

export interface FinancialLedgerItem {
  id: string;
  source: FinancialLedgerSource;
  type: FinancialEntryType;
  status: FinancialEntryStatus;
  category: string;
  description: string;
  amount: number;
  paymentMethod: PaymentMethod;
  effectiveDate: string;
  notes?: string;
  createdAtUtc: string;
  appointmentId?: string;
  clientId?: string;
  clientName?: string;
  procedureName?: string;
  appointmentStatus?: AppointmentStatus;
  paymentStatus?: PaymentStatus;
}

export interface FinancialCashFlowDay {
  date: string;
  income: number;
  expense: number;
}

export interface FinancialCategorySummary {
  category: string;
  amount: number;
}

export interface OutstandingReceivable {
  appointmentId: string;
  clientId: string;
  clientName: string;
  procedureName: string;
  scheduledDateTime: string;
  status: AppointmentStatus;
  totalPrice: number;
  amountPaid: number;
  outstandingAmount: number;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  isOverdue: boolean;
}

export interface FinanceOverview {
  from: string;
  to: string;
  appointmentIncome: number;
  manualIncome: number;
  manualExpense: number;
  netCash: number;
  receivable: number;
  plannedIncome: number;
  plannedExpense: number;
  projectedBalance: number;
  pendingReceivables: number;
  cashFlow: FinancialCashFlowDay[];
  outstandingReceivables: OutstandingReceivable[];
  expenseCategories: FinancialCategorySummary[];
}
