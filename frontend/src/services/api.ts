import {
  AccessCatalog,
  AdminUser,
  Appointment,
  AppointmentStatus,
  Client,
  ClientSummary,
  ClientDocument,
  ClientPhoto,
  ClinicOperationalSettings,
  ClinicalStorageUsage,
  FormSchemaDefinition,
  FormAnswers,
  FormSignatureCapture,
  FormSubmission,
  FormSubmissionSummary,
  FormTemplateDetail,
  FormTemplateSummary,
  FormVersion,
  AvailableFormVersion,
  CmsImageUpload,
  CmsContent,
  CustomReportRequest,
  CustomReportResult,
  DashboardStats,
  FinanceOverview,
  FinancialEntry,
  FinancialEntryStatus,
  FinancialEntryType,
  FinancialLedgerItem,
  FinancialLedgerSource,
  GoogleCalendarConnection,
  GoogleCalendarEventsResult,
  GoogleCalendarListItem,
  Lead,
  LoginResponse,
  MedicalRecord,
  MfaRecoveryCodes,
  MfaSetup,
  PagedResult,
  PaymentMethod,
  ProcedureType,
  StorageUsage,
  User,
  UserPermissionOverride,
  AvailableTermVersion,
  TermSubmission,
  TermSubmissionSummary,
  TermTemplateDetail,
  TermTemplateSummary,
  TermVersion,
  TermLayoutDefinition,
  TermInkCapture,
} from '../types';

const configuredApiBase = import.meta.env.VITE_API_URL?.trim() || '/api';
const API_BASE = configuredApiBase.replace(/\/$/, '');
export const UNAUTHORIZED_EVENT = 'estetica:unauthorized';

let csrfToken: string | null = null;

export class ApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

interface RequestOptions extends RequestInit {
  authenticated?: boolean;
  antiforgery?: boolean;
}

const readErrorMessage = async (response: Response): Promise<string> => {
  try {
    const body = await response.json() as { detail?: string; message?: string; title?: string; errors?: Record<string, string[]> };
    const validationMessage = body.errors
      ? Object.values(body.errors).flat().find(Boolean)
      : undefined;
    return body.detail || body.message || validationMessage || body.title || 'Não foi possível concluir a operação.';
  } catch {
    return response.status === 401
      ? 'Sua sessão é inválida ou expirou.'
      : 'Não foi possível concluir a operação.';
  }
};

const ensureCsrfToken = async (): Promise<string> => {
  if (csrfToken) return csrfToken;
  const response = await fetch(`${API_BASE}/auth/csrf`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) throw new ApiError(response.status, await readErrorMessage(response));
  const body = await response.json() as { token: string };
  csrfToken = body.token;
  return body.token;
};

const request = async <T>(path: string, options: RequestOptions = {}): Promise<T> => {
  const {
    authenticated = false,
    antiforgery = true,
    headers: initialHeaders,
    ...fetchOptions
  } = options;
  const headers = new Headers(initialHeaders);
  const method = (fetchOptions.method || 'GET').toUpperCase();

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method) && antiforgery) {
    headers.set('X-CSRF-TOKEN', await ensureCsrfToken());
  }
  if (fetchOptions.body && !(fetchOptions.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      ...fetchOptions,
      credentials: 'include',
      headers,
    });
  } catch {
    throw new ApiError(0, 'O servidor está indisponível. Verifique sua conexão e tente novamente.');
  }

  if (!response.ok) {
    const message = await readErrorMessage(response);
    if (response.status === 401 && authenticated) {
      csrfToken = null;
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return await response.json() as T;
};

const resetCsrf = () => { csrfToken = null; };
const pageAll = <T>(result: PagedResult<T>) => result.items;

const privatePdf = async (path: string): Promise<ArrayBuffer> => {
  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, { credentials: 'include' });
  } catch {
    throw new ApiError(0, 'O servidor está indisponível. Verifique sua conexão e tente novamente.');
  }
  if (!response.ok) {
    if (response.status === 401) {
      csrfToken = null;
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
    }
    throw new ApiError(response.status, await readErrorMessage(response));
  }
  return response.arrayBuffer();
};

export const api = {
  login: async (username: string, password: string): Promise<LoginResponse> => {
    const result = await request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    });
    resetCsrf();
    return result;
  },

  loginMfa: async (code: string): Promise<LoginResponse> => {
    const result = await request<LoginResponse>('/auth/login/mfa', {
      method: 'POST',
      body: JSON.stringify({ code, rememberMachine: false }),
    });
    resetCsrf();
    return result;
  },

  loginRecovery: async (code: string): Promise<LoginResponse> => {
    const result = await request<LoginResponse>('/auth/login/recovery', {
      method: 'POST',
      body: JSON.stringify({ code }),
    });
    resetCsrf();
    return result;
  },

  me: (): Promise<User> => request<User>('/auth/me', { authenticated: true }),

  logout: async (): Promise<void> => {
    try {
      await request<void>('/auth/logout', { method: 'POST', authenticated: true });
    } finally {
      resetCsrf();
    }
  },

  changePassword: (currentPassword: string, newPassword: string): Promise<void> =>
    request<void>('/auth/change-password', {
      method: 'POST',
      authenticated: true,
      body: JSON.stringify({ currentPassword, newPassword }),
    }),

  setupMfa: (): Promise<MfaSetup> => request<MfaSetup>('/auth/mfa/setup', {
    method: 'POST', authenticated: true,
  }),

  enableMfa: (code: string): Promise<MfaRecoveryCodes> => request<MfaRecoveryCodes>('/auth/mfa/enable', {
    method: 'POST',
    authenticated: true,
    body: JSON.stringify({ code }),
  }),

  regenerateRecoveryCodes: (): Promise<MfaRecoveryCodes> => request<MfaRecoveryCodes>(
    '/auth/mfa/recovery-codes', { method: 'POST', authenticated: true },
  ),

  getCms: (): Promise<CmsContent> => request<CmsContent>('/public/cms'),

  updateCms: (data: CmsContent): Promise<CmsContent> => request<CmsContent>('/cms', {
    method: 'PUT', authenticated: true, body: JSON.stringify(data),
  }),

  uploadCmsPhoto: (file: File): Promise<CmsContent> => {
    const formData = new FormData();
    formData.set('file', file);
    return request<CmsContent>('/cms/photo', {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  uploadCmsLogo: (variant: 'dark' | 'light', file: File): Promise<CmsContent> => {
    const formData = new FormData();
    formData.set('file', file);
    return request<CmsContent>(`/cms/logo/${variant}`, {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  uploadCmsImage: (file: File): Promise<CmsImageUpload> => {
    const formData = new FormData();
    formData.set('file', file);
    return request<CmsImageUpload>('/cms/images', {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  getProcedures: (publicOnly = false): Promise<ProcedureType[]> => request<ProcedureType[]>(
    publicOnly ? '/public/procedures' : '/procedures',
    { authenticated: !publicOnly },
  ),

  getGoogleCalendarConnection: (): Promise<GoogleCalendarConnection> => request<GoogleCalendarConnection>(
    '/integrations/google-calendar/connection', { authenticated: true },
  ),

  getClinicOperationalSettings: (): Promise<ClinicOperationalSettings> => request<ClinicOperationalSettings>(
    '/settings/operational', { authenticated: true },
  ),

  saveClinicOperatingHours: (operatingHours: ClinicOperationalSettings['operatingHours']): Promise<ClinicOperationalSettings> =>
    request<ClinicOperationalSettings>('/settings/operational', {
      method: 'PUT',
      authenticated: true,
      // TimeOnly no backend é serializado no formato ISO com segundos. O input HTML entrega HH:mm.
      body: JSON.stringify({
        operatingHours: operatingHours.map(day => ({
          ...day,
          startTime: day.startTime.length === 5 ? `${day.startTime}:00` : day.startTime,
          endTime: day.endTime.length === 5 ? `${day.endTime}:00` : day.endTime,
        })),
      }),
    }),

  getStorageUsage: (): Promise<StorageUsage> => request<StorageUsage>(
    '/settings/storage', { authenticated: true },
  ),

  startGoogleCalendarConnection: (): Promise<{ authorizationUrl: string }> => request<{ authorizationUrl: string }>(
    '/integrations/google-calendar/connect', { method: 'POST', authenticated: true },
  ),

  getGoogleCalendars: (): Promise<GoogleCalendarListItem[]> => request<GoogleCalendarListItem[]>(
    '/integrations/google-calendar/calendars', { authenticated: true },
  ),

  selectGoogleCalendar: (calendarId: string): Promise<GoogleCalendarConnection> => request<GoogleCalendarConnection>(
    '/integrations/google-calendar/calendar', {
      method: 'PUT', authenticated: true, body: JSON.stringify({ calendarId }),
    },
  ),

  disconnectGoogleCalendar: (): Promise<void> => request<void>('/integrations/google-calendar/connection', {
    method: 'DELETE', authenticated: true,
  }),

  getGoogleCalendarEvents: (fromUtc: string, toUtc: string): Promise<GoogleCalendarEventsResult> =>
    request<GoogleCalendarEventsResult>(
      `/integrations/google-calendar/events?fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}`,
      { authenticated: true },
    ),

  saveProcedure: (procedure: Partial<ProcedureType>): Promise<ProcedureType> => {
    const isEdit = Boolean(procedure.id);
    return request<ProcedureType>(isEdit ? `/procedures/${procedure.id}` : '/procedures', {
      method: isEdit ? 'PUT' : 'POST', authenticated: true, body: JSON.stringify(procedure),
    });
  },

  uploadProcedureImage: (procedureId: string, file: File): Promise<ProcedureType> => {
    const formData = new FormData();
    formData.set('file', file);
    return request<ProcedureType>(`/procedures/${encodeURIComponent(procedureId)}/image`, {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  getClients: async (): Promise<Client[]> => pageAll(await request<PagedResult<Client>>(
    '/clients?page=1&pageSize=100', { authenticated: true },
  )),

  getClientById: async (id: string): Promise<Client> => {
    const result = await request<{
      client: Client;
      medicalRecords: MedicalRecord[];
      photos: Client['photos'];
      documents: Client['documents'];
    }>(`/clients/${encodeURIComponent(id)}/clinical`, { authenticated: true });
    return {
      ...result.client,
      medicalRecords: result.medicalRecords,
      photos: result.photos,
      documents: result.documents,
    };
  },

  getClientSummary: (clientId: string): Promise<ClientSummary> => request<ClientSummary>(
    `/clients/${encodeURIComponent(clientId)}/summary`, { authenticated: true },
  ),

  saveClient: (client: Partial<Client>): Promise<Client> => {
    const isEdit = Boolean(client.id);
    return request<Client>(isEdit ? `/clients/${client.id}` : '/clients', {
      method: isEdit ? 'PUT' : 'POST', authenticated: true, body: JSON.stringify(client),
    });
  },

  createMedicalRecord: (clientId: string, record: Partial<MedicalRecord>): Promise<MedicalRecord> =>
    request<MedicalRecord>(`/clients/${clientId}/medical-records`, {
      method: 'POST', authenticated: true, body: JSON.stringify(record),
    }),

  uploadClientPhoto: (
    clientId: string,
    data: {
      file: File;
      type: ClientPhoto['type'];
      procedureName: string;
      isPublicForWebsite: boolean;
      consentGiven: boolean;
    },
  ): Promise<ClientPhoto> => {
    const formData = new FormData();
    formData.set('file', data.file);
    formData.set('type', data.type);
    formData.set('procedureName', data.procedureName);
    formData.set('isPublicForWebsite', String(data.isPublicForWebsite));
    formData.set('consentGiven', String(data.consentGiven));
    return request<ClientPhoto>(`/clients/${encodeURIComponent(clientId)}/photos`, {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  uploadClientDocument: (
    clientId: string,
    data: { file: File; documentType: string },
  ): Promise<ClientDocument> => {
    const formData = new FormData();
    formData.set('file', data.file);
    formData.set('documentType', data.documentType);
    return request<ClientDocument>(`/clients/${encodeURIComponent(clientId)}/documents`, {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  getClientStorageUsage: (clientId: string): Promise<ClinicalStorageUsage> =>
    request<ClinicalStorageUsage>(`/clients/${encodeURIComponent(clientId)}/storage-usage`, {
      authenticated: true,
    }),

  getForms: (includeArchived = false): Promise<FormTemplateSummary[]> =>
    request<FormTemplateSummary[]>(`/forms?includeArchived=${includeArchived}`, { authenticated: true }),

  getForm: (id: string): Promise<FormTemplateDetail> =>
    request<FormTemplateDetail>(`/forms/${encodeURIComponent(id)}`, { authenticated: true }),

  getFormVersion: (id: string, versionNumber: number): Promise<FormVersion> =>
    request<FormVersion>(`/forms/${encodeURIComponent(id)}/versions/${versionNumber}`, { authenticated: true }),

  createForm: (data: { name: string; category: string; description: string }): Promise<FormTemplateDetail> =>
    request<FormTemplateDetail>('/forms', {
      method: 'POST', authenticated: true, body: JSON.stringify(data),
    }),

  updateFormDraft: (
    id: string,
    data: {
      draftRevision: number;
      name: string;
      category: string;
      description: string;
      schema: FormSchemaDefinition;
    },
  ): Promise<FormTemplateDetail> => request<FormTemplateDetail>(`/forms/${encodeURIComponent(id)}/draft`, {
    method: 'PUT', authenticated: true, body: JSON.stringify(data),
  }),

  publishForm: (id: string, draftRevision: number, changeSummary: string): Promise<FormVersion> =>
    request<FormVersion>(`/forms/${encodeURIComponent(id)}/publish`, {
      method: 'POST', authenticated: true, body: JSON.stringify({ draftRevision, changeSummary }),
    }),

  duplicateForm: (id: string, name: string): Promise<FormTemplateDetail> =>
    request<FormTemplateDetail>(`/forms/${encodeURIComponent(id)}/duplicate`, {
      method: 'POST', authenticated: true, body: JSON.stringify({ name }),
    }),

  archiveForm: (id: string): Promise<FormTemplateDetail> =>
    request<FormTemplateDetail>(`/forms/${encodeURIComponent(id)}/archive`, {
      method: 'POST', authenticated: true,
    }),

  getAvailableForms: (): Promise<AvailableFormVersion[]> =>
    request<AvailableFormVersion[]>('/forms/available', { authenticated: true }),

  getClientFormSubmissions: (clientId: string): Promise<FormSubmissionSummary[]> =>
    request<FormSubmissionSummary[]>(`/clients/${encodeURIComponent(clientId)}/form-submissions`, {
      authenticated: true,
    }),

  getClientFormSubmission: (clientId: string, submissionId: string): Promise<FormSubmission> =>
    request<FormSubmission>(
      `/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}`,
      { authenticated: true },
    ),

  createClientFormSubmission: (
    clientId: string,
    formVersionId: string,
    appointmentId?: string,
  ): Promise<FormSubmission> => request<FormSubmission>(
    `/clients/${encodeURIComponent(clientId)}/form-submissions`,
    {
      method: 'POST',
      authenticated: true,
      body: JSON.stringify({ formVersionId, appointmentId: appointmentId || null }),
    },
  ),

  updateClientFormSubmission: (
    clientId: string,
    submissionId: string,
    revision: number,
    answers: FormAnswers,
  ): Promise<FormSubmission> => request<FormSubmission>(
    `/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}/draft`,
    {
      method: 'PUT', authenticated: true, body: JSON.stringify({ revision, answers }),
    },
  ),

  deleteClientFormSubmissionDraft: (
    clientId: string,
    submissionId: string,
    revision: number,
  ): Promise<void> => request<void>(
    `/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}?revision=${encodeURIComponent(String(revision))}`,
    { method: 'DELETE', authenticated: true },
  ),

  finalizeClientFormSubmission: (
    clientId: string,
    submissionId: string,
    revision: number,
    answers: FormAnswers,
    signatures: FormSignatureCapture[],
  ): Promise<FormSubmission> => request<FormSubmission>(
    `/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}/finalize`,
    {
      method: 'POST', authenticated: true, body: JSON.stringify({ revision, answers, signatures }),
    },
  ),

  amendClientFormSubmission: (
    clientId: string,
    submissionId: string,
    revision: number,
    reason: string,
    answers: FormAnswers,
    signatures: FormSignatureCapture[],
  ): Promise<FormSubmission> => request<FormSubmission>(
    `/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}/amend`,
    {
      method: 'POST', authenticated: true, body: JSON.stringify({ revision, reason, answers, signatures }),
    },
  ),

  voidClientFormSubmission: (
    clientId: string,
    submissionId: string,
    revision: number,
    reason: string,
  ): Promise<FormSubmission> => request<FormSubmission>(
    `/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}/void`,
    {
      method: 'POST', authenticated: true, body: JSON.stringify({ revision, reason }),
    },
  ),

  getClientFormPdf: (clientId: string, submissionId: string): Promise<ArrayBuffer> =>
    privatePdf(`/clients/${encodeURIComponent(clientId)}/form-submissions/${encodeURIComponent(submissionId)}/content`),

  getTerms: (includeArchived = false): Promise<TermTemplateSummary[]> =>
    request<TermTemplateSummary[]>(`/terms?includeArchived=${includeArchived}`, { authenticated: true }),

  getTerm: (id: string): Promise<TermTemplateDetail> =>
    request<TermTemplateDetail>(`/terms/${encodeURIComponent(id)}`, { authenticated: true }),

  getTermVersion: (id: string, versionNumber: number): Promise<TermVersion> =>
    request<TermVersion>(`/terms/${encodeURIComponent(id)}/versions/${versionNumber}`, { authenticated: true }),

  getTermDraftPdf: (id: string): Promise<ArrayBuffer> =>
    privatePdf(`/terms/${encodeURIComponent(id)}/draft-pdf/content`),

  getTermVersionPdf: (id: string, versionNumber: number): Promise<ArrayBuffer> =>
    privatePdf(`/terms/${encodeURIComponent(id)}/versions/${versionNumber}/content`),

  createTerm: (data: { name: string; description: string }): Promise<TermTemplateDetail> =>
    request<TermTemplateDetail>('/terms', { method: 'POST', authenticated: true, body: JSON.stringify(data) }),

  uploadTermDraftPdf: (id: string, file: File, draftRevision: number): Promise<TermTemplateDetail> => {
    const formData = new FormData();
    formData.set('file', file);
    formData.set('draftRevision', String(draftRevision));
    return request<TermTemplateDetail>(`/terms/${encodeURIComponent(id)}/draft-pdf`, {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  updateTermDraft: (
    id: string,
    data: { draftRevision: number; name: string; description: string; layout: TermLayoutDefinition },
  ): Promise<TermTemplateDetail> => request<TermTemplateDetail>(`/terms/${encodeURIComponent(id)}/draft`, {
    method: 'PUT', authenticated: true, body: JSON.stringify(data),
  }),

  publishTerm: (id: string, draftRevision: number, changeSummary: string): Promise<TermVersion> =>
    request<TermVersion>(`/terms/${encodeURIComponent(id)}/publish`, {
      method: 'POST', authenticated: true, body: JSON.stringify({ draftRevision, changeSummary }),
    }),

  archiveTerm: (id: string): Promise<TermTemplateDetail> =>
    request<TermTemplateDetail>(`/terms/${encodeURIComponent(id)}/archive`, { method: 'POST', authenticated: true }),

  getAvailableTerms: (): Promise<AvailableTermVersion[]> =>
    request<AvailableTermVersion[]>('/terms/available', { authenticated: true }),

  getClientTermSubmissions: (clientId: string): Promise<TermSubmissionSummary[]> =>
    request<TermSubmissionSummary[]>(`/clients/${encodeURIComponent(clientId)}/term-submissions`, { authenticated: true }),

  getClientTermSubmission: (clientId: string, submissionId: string): Promise<TermSubmission> =>
    request<TermSubmission>(`/clients/${encodeURIComponent(clientId)}/term-submissions/${encodeURIComponent(submissionId)}`, { authenticated: true }),

  createClientTermSubmission: (clientId: string, termVersionId: string, appointmentId?: string): Promise<TermSubmission> =>
    request<TermSubmission>(`/clients/${encodeURIComponent(clientId)}/term-submissions`, {
      method: 'POST', authenticated: true, body: JSON.stringify({ termVersionId, appointmentId: appointmentId || null }),
    }),

  updateClientTermSubmission: (
    clientId: string,
    submissionId: string,
    revision: number,
    values: Record<string, string | boolean | null>,
    ink: TermInkCapture[],
  ): Promise<TermSubmission> => request<TermSubmission>(
    `/clients/${encodeURIComponent(clientId)}/term-submissions/${encodeURIComponent(submissionId)}/draft`,
    { method: 'PUT', authenticated: true, body: JSON.stringify({ revision, values, ink }) },
  ),

  deleteClientTermSubmissionDraft: (clientId: string, submissionId: string, revision: number): Promise<void> =>
    request<void>(`/clients/${encodeURIComponent(clientId)}/term-submissions/${encodeURIComponent(submissionId)}?revision=${encodeURIComponent(String(revision))}`, {
      method: 'DELETE', authenticated: true,
    }),

  finalizeClientTermSubmission: (
    clientId: string,
    submissionId: string,
    revision: number,
    values: Record<string, string | boolean | null>,
    ink: TermInkCapture[],
    finalPdf: File,
  ): Promise<TermSubmission> => {
    const formData = new FormData();
    formData.set('file', finalPdf);
    formData.set('payload', JSON.stringify({ revision, values, ink }));
    return request<TermSubmission>(`/clients/${encodeURIComponent(clientId)}/term-submissions/${encodeURIComponent(submissionId)}/finalize`, {
      method: 'POST', authenticated: true, body: formData,
    });
  },

  voidClientTermSubmission: (clientId: string, submissionId: string, revision: number, reason: string): Promise<TermSubmission> =>
    request<TermSubmission>(`/clients/${encodeURIComponent(clientId)}/term-submissions/${encodeURIComponent(submissionId)}/void`, {
      method: 'POST', authenticated: true, body: JSON.stringify({ revision, reason }),
    }),

  getClientTermPdf: (clientId: string, submissionId: string): Promise<ArrayBuffer> =>
    privatePdf(`/clients/${encodeURIComponent(clientId)}/term-submissions/${encodeURIComponent(submissionId)}/content`),

  getAppointments: async (): Promise<Appointment[]> => pageAll(await request<PagedResult<Appointment>>(
    '/appointments?page=1&pageSize=100', { authenticated: true },
  )),

  saveAppointment: (appointment: Partial<Appointment>): Promise<Appointment> => {
    const isEdit = Boolean(appointment.id);
    return request<Appointment>(isEdit ? `/appointments/${appointment.id}` : '/appointments', {
      method: isEdit ? 'PUT' : 'POST', authenticated: true, body: JSON.stringify(appointment),
    });
  },

  deactivateProcedure: (id: string): Promise<void> => request<void>(`/procedures/${encodeURIComponent(id)}`, {
    method: 'DELETE', authenticated: true,
  }),

  updateAppointmentStatus: (
    id: string,
    status: AppointmentStatus,
    amountPaid?: number,
    paymentMethod?: PaymentMethod,
  ): Promise<Appointment> => request<Appointment>(`/appointments/${id}/status`, {
    method: 'PUT', authenticated: true, body: JSON.stringify({ status, amountPaid, paymentMethod }),
  }),

  createLead: (lead: Partial<Lead>): Promise<{ message: string }> => request<{ message: string }>(
    '/public/leads', { method: 'POST', body: JSON.stringify(lead), antiforgery: false },
  ),

  getLeads: async (): Promise<Lead[]> => pageAll(await request<PagedResult<Lead>>(
    '/leads?page=1&pageSize=100', { authenticated: true },
  )),

  updateLeadStatus: (id: string, status: string): Promise<Lead> => request<Lead>(
    `/leads/${encodeURIComponent(id)}/status`, {
      method: 'PUT', authenticated: true, body: JSON.stringify({ status }),
    },
  ),

  getDashboardStats: (): Promise<DashboardStats> => request<DashboardStats>(
    '/reports/dashboard', { authenticated: true },
  ),

  runCustomReport: (data: CustomReportRequest): Promise<CustomReportResult> => request<CustomReportResult>(
    '/reports/custom', { method: 'POST', authenticated: true, body: JSON.stringify(data) },
  ),

  exportCustomReport: async (data: CustomReportRequest): Promise<{ blob: Blob; fileName: string }> => {
    const token = await ensureCsrfToken();
    const response = await fetch(`${API_BASE}/reports/custom/export`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      if (response.status === 401) window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
      throw new ApiError(response.status, await readErrorMessage(response));
    }
    const disposition = response.headers.get('content-disposition') ?? '';
    const fileName = disposition.match(/filename=\"?([^\";]+)\"?/i)?.[1] ?? 'relatorio-clinica.csv';
    return { blob: await response.blob(), fileName };
  },

  getFinanceOverview: (from?: string, to?: string): Promise<FinanceOverview> => {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const query = params.size ? `?${params.toString()}` : '';
    return request<FinanceOverview>(`/finance/overview${query}`, { authenticated: true });
  },

  getFinancialEntries: async (from?: string, to?: string, search = ''): Promise<FinancialEntry[]> => {
    const params = new URLSearchParams({ page: '1', pageSize: '100' });
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    if (search.trim()) params.set('search', search.trim());
    return pageAll(await request<PagedResult<FinancialEntry>>(`/finance/entries?${params.toString()}`, { authenticated: true }));
  },

  getFinancialLedger: async (filters: {
    from?: string;
    to?: string;
    source?: FinancialLedgerSource;
    type?: FinancialEntryType;
    status?: FinancialEntryStatus;
    category?: string;
    search?: string;
  } = {}): Promise<FinancialLedgerItem[]> => {
    const params = new URLSearchParams({ page: '1', pageSize: '100' });
    if (filters.from) params.set('from', filters.from);
    if (filters.to) params.set('to', filters.to);
    if (filters.source) params.set('source', filters.source);
    if (filters.type) params.set('type', filters.type);
    if (filters.status) params.set('status', filters.status);
    if (filters.category?.trim()) params.set('category', filters.category.trim());
    if (filters.search?.trim()) params.set('search', filters.search.trim());
    return pageAll(await request<PagedResult<FinancialLedgerItem>>(`/finance/ledger?${params.toString()}`, { authenticated: true }));
  },

  saveFinancialEntry: (entry: Partial<FinancialEntry>): Promise<FinancialEntry> => {
    const isEdit = Boolean(entry.id);
    return request<FinancialEntry>(isEdit ? `/finance/entries/${entry.id}` : '/finance/entries', {
      method: isEdit ? 'PUT' : 'POST', authenticated: true, body: JSON.stringify(entry),
    });
  },

  settleFinancialEntry: (id: string, effectiveDate: string): Promise<FinancialEntry> => request<FinancialEntry>(
    `/finance/entries/${encodeURIComponent(id)}/settle`, {
      method: 'POST', authenticated: true, body: JSON.stringify({ effectiveDate }),
    },
  ),

  cancelFinancialEntry: (id: string): Promise<void> => request<void>(`/finance/entries/${encodeURIComponent(id)}`, {
    method: 'DELETE', authenticated: true,
  }),

  getUsers: (search = ''): Promise<PagedResult<AdminUser>> => request<PagedResult<AdminUser>>(
    `/users?page=1&pageSize=100&search=${encodeURIComponent(search)}`,
    { authenticated: true },
  ),

  getAccessCatalog: (): Promise<AccessCatalog> => request<AccessCatalog>(
    '/users/access-catalog', { authenticated: true },
  ),

  createUser: (data: { username: string; email: string; temporaryPassword: string; roles: string[] }): Promise<AdminUser> =>
    request<AdminUser>('/users', {
      method: 'POST', authenticated: true, body: JSON.stringify(data),
    }),

  updateUserAccess: (
    id: string,
    roles: string[],
    permissionOverrides: UserPermissionOverride[],
  ): Promise<AdminUser> => request<AdminUser>(`/users/${id}/access`, {
    method: 'PUT', authenticated: true, body: JSON.stringify({ roles, permissionOverrides }),
  }),

  updateUserStatus: (id: string, isActive: boolean): Promise<AdminUser> =>
    request<AdminUser>(`/users/${id}/status`, {
      method: 'PUT', authenticated: true, body: JSON.stringify({ isActive }),
    }),

  resetUserPassword: (id: string): Promise<{ temporaryPassword: string }> =>
    request<{ temporaryPassword: string }>(`/users/${id}/reset-password`, {
      method: 'POST', authenticated: true,
    }),
};
