import React, { useEffect, useRef, useState } from 'react';
import { Download, FileSignature, History, LockKeyhole, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import {
  AvailableFormVersion,
  FormAnswers,
  FormFieldDefinition,
  FormSignature,
  FormSignatureCapture,
  FormSubmission,
  FormSubmissionStatus,
  FormSubmissionSummary,
  SignaturePoint,
} from '../../types';
import { api } from '../../services/api';
import { formatClinicDateTime } from '../../utils/clinicDateTime';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, Dialog, Select } from '../ui/Components';

const statusLabels: Record<FormSubmissionStatus, string> = {
  Draft: 'Rascunho',
  Finalized: 'Finalizado',
  Amended: 'Com adendo',
  Voided: 'Anulado',
};

const statusVariants: Record<FormSubmissionStatus, 'warning' | 'success' | 'info' | 'danger'> = {
  Draft: 'warning',
  Finalized: 'success',
  Amended: 'info',
  Voided: 'danger',
};

const declaration = 'Declaro que li e concordo com as informações e autorizações apresentadas neste formulário.';

interface InlineSignatureCanvasProps {
  field: FormFieldDefinition;
  initialSignerName: string;
  initialCapture?: FormSignatureCapture;
  onConfirm: (capture: FormSignatureCapture) => void;
}

const InlineSignatureCanvas: React.FC<InlineSignatureCanvasProps> = ({ field, initialSignerName, initialCapture, onConfirm }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const strokesRef = useRef<SignaturePoint[][]>(initialCapture?.strokes.map(stroke => [...stroke]) ?? []);
  const activeStrokeRef = useRef<SignaturePoint[] | null>(null);
  const pointerTypeRef = useRef<FormSignatureCapture['pointerType']>(initialCapture?.pointerType ?? 'unknown');
  const [signerName, setSignerName] = useState(initialCapture?.signerName ?? initialSignerName);
  const [accepted, setAccepted] = useState(false);
  const [strokeCount, setStrokeCount] = useState(initialCapture?.strokes.filter(stroke => stroke.length >= 2).length ?? 0);

  const redraw = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = Math.max(1, Math.floor(rect.width * ratio));
    canvas.height = Math.max(1, Math.floor(rect.height * ratio));
    const context = canvas.getContext('2d');
    if (!context) return;
    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.strokeStyle = '#0f172a';
    context.lineWidth = Math.max(3, 3 * ratio);
    context.lineCap = 'round';
    context.lineJoin = 'round';
    for (const stroke of strokesRef.current) for (let index = 1; index < stroke.length; index++) {
      const previous = stroke[index - 1];
      const current = stroke[index];
      context.beginPath();
      context.moveTo(previous.x * canvas.width, previous.y * canvas.height);
      context.lineTo(current.x * canvas.width, current.y * canvas.height);
      context.stroke();
    }
  };

  useEffect(() => {
    strokesRef.current = initialCapture?.strokes.map(stroke => [...stroke]) ?? [];
    activeStrokeRef.current = null;
    pointerTypeRef.current = initialCapture?.pointerType ?? 'unknown';
    setSignerName(initialCapture?.signerName ?? initialSignerName);
    setAccepted(false);
    setStrokeCount(strokesRef.current.filter(stroke => stroke.length >= 2).length);
    redraw();
    const canvas = canvasRef.current;
    if (!canvas) return undefined;
    const observer = new ResizeObserver(redraw);
    observer.observe(canvas);
    return () => {
      observer.disconnect();
    };
  }, [field.id, initialCapture, initialSignerName]);

  const pointFromEvent = (event: React.PointerEvent<HTMLCanvasElement>): SignaturePoint => {
    const rect = event.currentTarget.getBoundingClientRect();
    return {
      x: Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)),
      y: Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height)),
    };
  };

  const startStroke = (event: React.PointerEvent<HTMLCanvasElement>) => {
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    pointerTypeRef.current = event.pointerType === 'touch' || event.pointerType === 'pen' || event.pointerType === 'mouse'
      ? event.pointerType
      : 'unknown';
    const stroke = [pointFromEvent(event)];
    strokesRef.current.push(stroke);
    activeStrokeRef.current = stroke;
    setStrokeCount(strokesRef.current.length);
  };

  const continueStroke = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const stroke = activeStrokeRef.current;
    if (!stroke) return;
    event.preventDefault();
    const current = pointFromEvent(event);
    const previous = stroke[stroke.length - 1];
    stroke.push(current);
    const canvas = event.currentTarget;
    const context = canvas.getContext('2d');
    if (!context) return;
    context.beginPath();
    context.moveTo(previous.x * canvas.width, previous.y * canvas.height);
    context.lineTo(current.x * canvas.width, current.y * canvas.height);
    context.stroke();
  };

  const endStroke = () => {
    if (activeStrokeRef.current && activeStrokeRef.current.length < 2) strokesRef.current.pop();
    activeStrokeRef.current = null;
    setStrokeCount(strokesRef.current.filter(stroke => stroke.length >= 2).length);
  };

  const clear = () => {
    strokesRef.current = [];
    activeStrokeRef.current = null;
    setStrokeCount(0);
    redraw();
  };

  const cancel = () => {
    strokesRef.current = initialCapture?.strokes.map(stroke => [...stroke]) ?? [];
    activeStrokeRef.current = null;
    pointerTypeRef.current = initialCapture?.pointerType ?? 'unknown';
    setSignerName(initialCapture?.signerName ?? initialSignerName);
    setAccepted(false);
    setStrokeCount(strokesRef.current.filter(stroke => stroke.length >= 2).length);
    redraw();
  };

  const confirm = () => {
    const canvas = canvasRef.current;
    if (!canvas || strokesRef.current.length === 0 || strokesRef.current.some(stroke => stroke.length < 2)) return;
    const rect = canvas.getBoundingClientRect();
    onConfirm({
      fieldId: field.id,
      signerName: signerName.trim(),
      pointerType: pointerTypeRef.current,
      canvasWidth: Math.round(rect.width),
      canvasHeight: Math.round(rect.height),
      strokes: strokesRef.current,
    });
  };

  return <div className="space-y-3 rounded-xl border border-slate-700 bg-slate-950/40 p-3">
    <div className="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto] md:items-start"><div className="overflow-hidden rounded-lg border-2 border-slate-500 bg-white shadow-inner"><canvas ref={canvasRef} className="block h-36 w-full touch-none cursor-crosshair bg-white sm:h-40" onPointerDown={startStroke} onPointerMove={continueStroke} onPointerUp={endStroke} onPointerCancel={endStroke} aria-label={`Área para desenhar ${field.label}`} /></div><div className="flex flex-wrap gap-2 rounded-xl border border-slate-600 bg-slate-950/95 p-2 shadow-xl md:flex-col md:items-stretch"><Button type="button" variant="outline" size="sm" onClick={clear} disabled={!strokeCount}><Trash2 className="h-3.5 w-3.5" /> Limpar</Button><Button type="button" variant="outline" size="sm" onClick={cancel}>Cancelar</Button><Button type="button" size="sm" onClick={confirm} disabled={!strokeCount || !accepted || signerName.trim().length < 2}><ShieldCheck className="h-3.5 w-3.5" /> Salvar assinatura</Button></div></div>
    <span className="block text-xs text-slate-400">{strokeCount ? `${strokeCount} traço(s) capturado(s).` : 'Use o dedo ou a caneta para assinar.'}</span>
    <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]"><label className="block text-xs font-medium text-slate-200">Nome do signatário<input value={signerName} onChange={event => setSignerName(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100 outline-none focus:border-rose-500" /></label><label className="flex items-start gap-2 rounded-lg border border-slate-700 bg-slate-900/60 p-3 text-xs text-slate-300"><input type="checkbox" className="mt-0.5" checked={accepted} onChange={event => setAccepted(event.target.checked)} /><span>{declaration}</span></label></div>
  </div>;
};

const FieldAnswer: React.FC<{
  field: FormFieldDefinition;
  answers: FormAnswers;
  setAnswer: (fieldId: string, value: FormAnswers[string] | undefined) => void;
  readOnly: boolean;
  signature?: FormSignature;
  capture?: FormSignatureCapture;
  clientName: string;
  onSignatureConfirm: (capture: FormSignatureCapture) => void;
}> = ({ field, answers, setAnswer, readOnly, signature, capture, clientName, onSignatureConfirm }) => {
  const value = answers[field.id];
  const label = <label className="mb-1.5 block text-xs font-medium text-slate-300">{field.label}{field.required && ' *'}</label>;
  if (field.type === 'Section') return <h4 className="border-b border-slate-800 pb-2 pt-3 font-display font-semibold text-rose-400">{field.label}</h4>;
  if (field.type === 'InformationalText') return <div className="rounded-xl border border-sky-500/20 bg-sky-500/5 p-3 text-xs text-sky-100"><strong>{field.label}</strong>{field.description && <p className="mt-1 text-sky-200/70">{field.description}</p>}</div>;

  if (readOnly) {
    if (field.type === 'Signature') return (
      <div className="space-y-2">
        {label}
        {signature ? <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3">
          <img src={signature.contentUrl} alt={`Assinatura de ${signature.signerName}`} className="h-28 w-full rounded-lg bg-white object-contain" />
          <p className="mt-2 text-[11px] text-emerald-300">{signature.signerName} • {new Date(signature.capturedAtUtc).toLocaleString('pt-BR')}</p>
        </div> : <p className="text-xs text-slate-500">Não assinada.</p>}
      </div>
    );
    const optionLabel = (id: string) => field.options.find(option => option.id === id)?.label || id;
    const display = Array.isArray(value) ? value.map(optionLabel).join(', ')
      : typeof value === 'boolean' ? (value ? 'Sim' : 'Não')
        : field.type === 'Dropdown' && typeof value === 'string' ? optionLabel(value)
          : value ?? 'Não respondido';
    return <div>{label}<div className="min-h-9 rounded-lg border border-slate-800 bg-slate-950/60 px-3 py-2 text-sm text-slate-200">{String(display)}</div>{field.description && <p className="mt-1 text-[11px] text-slate-500">{field.description}</p>}</div>;
  }

  if (field.type === 'Signature') return (
    <div className="space-y-2">
      {label}
      {field.description && <p className="text-[11px] text-slate-500">{field.description}</p>}
      <InlineSignatureCanvas field={field} initialSignerName={clientName} initialCapture={capture} onConfirm={onSignatureConfirm} />
      {capture && <p className="text-[11px] text-emerald-300">Assinatura de {capture.signerName} pronta para finalizar o formulário. Você pode redesenhá-la antes da confirmação final.</p>}
    </div>
  );

  if (field.type === 'LongText') return <div>{label}<textarea rows={4} className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm text-slate-100 focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50" placeholder={field.placeholder} value={typeof value === 'string' ? value : ''} onChange={event => setAnswer(field.id, event.target.value || undefined)} />{field.description && <p className="mt-1 text-[11px] text-slate-500">{field.description}</p>}</div>;
  if (field.type === 'YesNo') return <div>{label}<select className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm" value={typeof value === 'boolean' ? String(value) : ''} onChange={event => setAnswer(field.id, event.target.value === '' ? undefined : event.target.value === 'true')}><option value="">Selecione...</option><option value="true">Sim</option><option value="false">Não</option></select></div>;
  if (field.type === 'Checkbox') return <label className="flex items-start gap-3 rounded-xl border border-slate-800 p-3 text-xs text-slate-300"><input type="checkbox" className="mt-0.5" checked={value === true} onChange={event => setAnswer(field.id, event.target.checked)} /><span><strong>{field.label}{field.required && ' *'}</strong>{field.description && <span className="mt-1 block text-slate-500">{field.description}</span>}</span></label>;
  if (field.type === 'Dropdown') return <div>{label}<select className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm" value={typeof value === 'string' ? value : ''} onChange={event => setAnswer(field.id, event.target.value || undefined)}><option value="">Selecione...</option>{field.options.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}</select></div>;
  if (field.type === 'CheckboxGroup' || field.type === 'MultiSelect') {
    const selected = Array.isArray(value) ? value : [];
    return <fieldset className="space-y-2"><legend className="text-xs font-medium text-slate-300">{field.label}{field.required && ' *'}</legend>{field.options.map(option => <label key={option.id} className="flex items-center gap-2 text-xs text-slate-300"><input type="checkbox" checked={selected.includes(option.id)} onChange={event => setAnswer(field.id, event.target.checked ? [...selected, option.id] : selected.filter(id => id !== option.id))} />{option.label}</label>)}</fieldset>;
  }
  return <div>{label}<input type={field.type === 'Number' ? 'number' : field.type === 'Date' ? 'date' : 'text'} className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm text-slate-100 focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50" placeholder={field.placeholder} value={typeof value === 'string' || typeof value === 'number' ? value : ''} onChange={event => setAnswer(field.id, field.type === 'Number' ? (event.target.value === '' ? undefined : Number(event.target.value)) : event.target.value || undefined)} />{field.description && <p className="mt-1 text-[11px] text-slate-500">{field.description}</p>}</div>;
};

const SubmissionEditor: React.FC<{
  clientName: string;
  submission: FormSubmission;
  onClose: () => void;
  onChanged: (submission: FormSubmission) => void;
  onDeleted: () => void;
}> = ({ clientName, submission: initialSubmission, onClose, onChanged, onDeleted }) => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const canManage = can('clinical.manage');
  const [submission, setSubmission] = useState(initialSubmission);
  const [mode, setMode] = useState<'view' | 'edit' | 'amend'>(initialSubmission.status === 'Draft' && canManage ? 'edit' : 'view');
  const [answers, setAnswers] = useState<FormAnswers>(initialSubmission.status === 'Draft' ? initialSubmission.originalAnswers : initialSubmission.effectiveAnswers);
  const [captures, setCaptures] = useState<Record<string, FormSignatureCapture>>({});
  const [amendmentReason, setAmendmentReason] = useState('');
  const [voidReason, setVoidReason] = useState('');
  const [showVoid, setShowVoid] = useState(false);
  const [showDelete, setShowDelete] = useState(false);
  const [showOriginal, setShowOriginal] = useState(false);
  const [busy, setBusy] = useState(false);
  const editable = canManage && (mode === 'edit' || mode === 'amend');

  const updateSubmission = (next: FormSubmission) => {
    setSubmission(next);
    setAnswers(next.status === 'Draft' ? next.originalAnswers : next.effectiveAnswers);
    setCaptures({});
    onChanged(next);
  };

  const setAnswer = (fieldId: string, value: FormAnswers[string] | undefined) => {
    setAnswers(current => {
      const next = { ...current };
      if (value === undefined || Array.isArray(value) && value.length === 0) delete next[fieldId];
      else next[fieldId] = value;
      return next;
    });
  };

  const execute = async (operation: () => Promise<FormSubmission>, success: string) => {
    setBusy(true);
    try {
      const next = await operation();
      updateSubmission(next);
      setMode('view');
      setAmendmentReason('');
      setShowVoid(false);
      setVoidReason('');
      showToast(success, 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível concluir a operação.', 'error');
    } finally {
      setBusy(false);
    }
  };

  const saveDraft = () => execute(
    () => api.updateClientFormSubmission(submission.clientId, submission.id, submission.revision, answers),
    'Rascunho salvo.',
  );
  const finalize = () => execute(
    () => api.finalizeClientFormSubmission(submission.clientId, submission.id, submission.revision, answers, Object.values(captures)),
    'Formulário finalizado e PDF imutável salvo no prontuário.',
  );
  const amend = () => execute(
    () => api.amendClientFormSubmission(submission.clientId, submission.id, submission.revision, amendmentReason, answers, Object.values(captures)),
    'Adendo registrado sem alterar o conteúdo original.',
  );
  const voidSubmission = () => execute(
    () => api.voidClientFormSubmission(submission.clientId, submission.id, submission.revision, voidReason),
    'Preenchimento anulado com histórico preservado.',
  );
  const openFinalPdf = async () => {
    try {
      const bytes = await api.getClientFormPdf(submission.clientId, submission.id);
      const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
      window.open(url, '_blank', 'noopener,noreferrer');
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível abrir o PDF final.', 'error');
    }
  };

  const deleteDraft = async () => {
    setBusy(true);
    try {
      await api.deleteClientFormSubmissionDraft(submission.clientId, submission.id, submission.revision);
      showToast('Rascunho excluído da pasta do cliente.', 'success');
      onDeleted();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível excluir o rascunho.', 'error');
    } finally {
      setBusy(false);
    }
  };

  const displayedAnswers = showOriginal ? submission.originalAnswers : answers;
  const relevantHash = showOriginal ? submission.originalAnswersHash : submission.effectiveAnswersHash;

  return <>
    <Dialog isOpen onClose={busy ? () => undefined : onClose} title={`${submission.formName} — versão ${submission.versionNumber}`} maxWidth="max-w-5xl">
      <div className="space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-950/50 p-3">
          <div className="flex items-center gap-2"><Badge variant={statusVariants[submission.status]}>{statusLabels[submission.status]}</Badge><span className="text-xs text-slate-400">Revisão {submission.revision} • schema {submission.schemaHash.slice(0, 12)}…</span></div>
          {submission.finalizedAtUtc && <span className="text-xs text-slate-500">Finalizado em {new Date(submission.finalizedAtUtc).toLocaleString('pt-BR')}</span>}
        </div>
        {submission.status === 'Voided' && <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-3 text-xs text-rose-200"><strong>Registro anulado:</strong> {submission.voidReason}</div>}
        {mode === 'amend' && <div className="rounded-xl border border-amber-500/30 bg-amber-500/5 p-3"><label className="mb-1.5 block text-xs font-medium text-amber-200">Justificativa obrigatória do adendo</label><textarea rows={3} className="w-full rounded-lg border border-amber-500/20 bg-slate-950/70 px-3 py-2 text-sm" value={amendmentReason} onChange={event => setAmendmentReason(event.target.value)} /></div>}
        {submission.status === 'Amended' && mode === 'view' && <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-sky-500/20 bg-sky-500/5 p-3 text-xs text-sky-200"><span>{submission.amendments.length} adendo(s). A resposta original permanece preservada.</span><Button variant="outline" size="sm" onClick={() => setShowOriginal(value => !value)}>{showOriginal ? 'Ver versão vigente' : 'Ver respostas originais'}</Button></div>}
        <div className="grid gap-4 md:grid-cols-2">
          {submission.schema.fields.map(field => {
            const signatures = submission.signatures.filter(item => item.fieldId === field.id && item.answersHash === relevantHash);
            return <FieldAnswer key={field.id} field={field} answers={displayedAnswers} setAnswer={setAnswer} readOnly={!editable} signature={signatures.at(-1)} capture={captures[field.id]} clientName={clientName} onSignatureConfirm={capture => setCaptures(current => ({ ...current, [capture.fieldId]: capture }))} />;
          })}
        </div>
        {submission.signatures.length > 0 && <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3 text-[11px] text-emerald-200"><ShieldCheck className="mr-1 inline h-4 w-4" /> Evidências: {submission.signatures.length} assinatura(s), vinculadas aos hashes de respostas e schema.</div>}
        {submission.amendments.length > 0 && <div className="space-y-2"><h4 className="flex items-center gap-2 text-sm font-semibold text-slate-200"><History className="h-4 w-4 text-sky-400" /> Histórico de adendos</h4>{submission.amendments.map(item => <div key={item.id} className="rounded-lg border border-slate-800 bg-slate-950/50 p-3 text-xs"><strong className="text-sky-300">Adendo {item.amendmentNumber}</strong><span className="ml-2 text-slate-500">{new Date(item.createdAtUtc).toLocaleString('pt-BR')}</span><p className="mt-1 text-slate-300">{item.reason}</p><p className="mt-1 font-mono text-[10px] text-slate-600">{item.previousAnswersHash.slice(0, 12)}… → {item.answersHash.slice(0, 12)}…</p></div>)}</div>}
        {showVoid && <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-3"><label className="mb-1.5 block text-xs font-medium text-rose-200">Motivo da anulação</label><textarea rows={3} className="w-full rounded-lg border border-rose-500/20 bg-slate-950/70 px-3 py-2 text-sm" value={voidReason} onChange={event => setVoidReason(event.target.value)} /><div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" onClick={() => setShowVoid(false)}>Cancelar</Button><Button variant="danger" size="sm" disabled={voidReason.trim().length < 5 || busy} onClick={voidSubmission}>Confirmar anulação</Button></div></div>}
        {showDelete && <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-3 text-xs text-rose-100"><strong>Excluir este rascunho?</strong><p className="mt-1 text-rose-200/80">Ele será removido da pasta do cliente. Formulários finalizados ou assinados não podem ser excluídos.</p><div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" onClick={() => setShowDelete(false)} disabled={busy}>Cancelar</Button><Button variant="danger" size="sm" disabled={busy} onClick={deleteDraft}><Trash2 className="h-3.5 w-3.5" /> Excluir rascunho</Button></div></div>}
        <div className="flex flex-wrap justify-end gap-2 border-t border-slate-800 pt-4">
          <Button variant="outline" onClick={onClose} disabled={busy}>Fechar</Button>
          {submission.finalPdfContentUrl && <Button variant="outline" onClick={() => void openFinalPdf()}><Download className="h-4 w-4" /> Abrir PDF final</Button>}
          {mode === 'edit' && canManage && <><Button variant="danger" onClick={() => setShowDelete(true)} disabled={busy}><Trash2 className="h-4 w-4" /> Excluir rascunho</Button><Button variant="outline" onClick={saveDraft} disabled={busy}>Salvar rascunho</Button><Button onClick={finalize} disabled={busy}><LockKeyhole className="h-4 w-4" /> Finalizar</Button></>}
          {mode === 'amend' && canManage && <><Button variant="outline" onClick={() => { setMode('view'); setAnswers(submission.effectiveAnswers); setCaptures({}); }}>Cancelar adendo</Button><Button onClick={amend} disabled={busy || amendmentReason.trim().length < 5}>Registrar adendo</Button></>}
          {mode === 'view' && submission.status !== 'Voided' && canManage && <><Button variant="outline" onClick={() => { setMode('amend'); setShowOriginal(false); setAnswers(submission.effectiveAnswers); setCaptures({}); }}>Criar adendo</Button><Button variant="danger" onClick={() => setShowVoid(true)}>Anular</Button></>}
        </div>
      </div>
    </Dialog>
  </>;
};

export const ClientFormsPanel: React.FC<{ clientId: string; clientName: string }> = ({ clientId, clientName }) => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const [available, setAvailable] = useState<AvailableFormVersion[]>([]);
  const [submissions, setSubmissions] = useState<FormSubmissionSummary[]>([]);
  const [appointments, setAppointments] = useState<{ id: string; procedureName: string; scheduledDateTime: string }[]>([]);
  const [startOpen, setStartOpen] = useState(false);
  const [selectedFormVersionId, setSelectedFormVersionId] = useState('');
  const [selectedAppointmentId, setSelectedAppointmentId] = useState('');
  const [activeSubmission, setActiveSubmission] = useState<FormSubmission | null>(null);
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try {
      const [forms, items, allAppointments] = await Promise.all([
        api.getAvailableForms(),
        api.getClientFormSubmissions(clientId),
        api.getAppointments().catch(() => []),
      ]);
      setAvailable(forms);
      setSubmissions(items);
      setAppointments(allAppointments.filter(item => item.clientId === clientId));
      if (!selectedFormVersionId && forms[0]) setSelectedFormVersionId(forms[0].formVersionId);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar os formulários digitais.', 'error');
    }
  };

  useEffect(() => { void load(); }, [clientId]);

  const start = async () => {
    if (!selectedFormVersionId) return;
    setBusy(true);
    try {
      const created = await api.createClientFormSubmission(clientId, selectedFormVersionId, selectedAppointmentId || undefined);
      setStartOpen(false);
      setSelectedAppointmentId('');
      setActiveSubmission(created);
      await load();
      showToast('Rascunho criado com a versão publicada atual.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível iniciar o formulário.', 'error');
    } finally {
      setBusy(false);
    }
  };

  const open = async (id: string) => {
    setBusy(true);
    try {
      setActiveSubmission(await api.getClientFormSubmission(clientId, id));
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível abrir o preenchimento.', 'error');
    } finally {
      setBusy(false);
    }
  };

  return <>
    <Card className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-3">
        <div>
          <h3 className="flex items-center gap-2 font-display font-semibold text-rose-400"><FileSignature className="h-4 w-4" /> Formulários digitais versionados</h3>
          <p className="mt-1 text-[11px] text-slate-500">Respostas, versões, assinaturas, adendos e anulações permanecem rastreáveis.</p>
        </div>
        {can('clinical.manage') && <Button size="sm" onClick={() => setStartOpen(true)} disabled={!available.length}><Plus className="h-4 w-4" /> Iniciar formulário</Button>}
      </div>
      {!submissions.length ? <p className="py-4 text-center text-xs text-slate-500">Nenhum formulário digital vinculado a este paciente.</p> : <div className="grid gap-3 md:grid-cols-2">{submissions.map(item => <button key={item.id} type="button" onClick={() => void open(item.id)} className="rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-left transition hover:border-rose-500/40 hover:bg-slate-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-rose-500/60"><div className="flex items-start justify-between gap-3"><div><strong className="text-sm text-white">{item.formName}</strong><p className="mt-1 text-[11px] text-slate-500">Versão {item.versionNumber} • atualizado em {new Date(item.updatedAtUtc).toLocaleString('pt-BR')}</p></div><Badge variant={statusVariants[item.status]}>{statusLabels[item.status]}</Badge></div><div className="mt-2 flex gap-3 text-[10px] text-slate-500"><span>Revisão {item.revision}</span>{item.isSigned && <span className="text-emerald-400">✓ Assinado</span>}</div></button>)}</div>}
    </Card>

    <Dialog isOpen={startOpen} onClose={() => setStartOpen(false)} title="Iniciar formulário publicado" maxWidth="max-w-2xl">
      <div className="space-y-4">
        <Select label="Modelo e versão" value={selectedFormVersionId} onChange={event => setSelectedFormVersionId(event.target.value)}>{available.map(item => <option key={item.formVersionId} value={item.formVersionId}>{item.name} — versão {item.versionNumber}</option>)}</Select>
        {available.find(item => item.formVersionId === selectedFormVersionId) && <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-xs text-slate-400">{available.find(item => item.formVersionId === selectedFormVersionId)?.description || 'Sem descrição.'}<p className="mt-1 text-[10px] text-slate-600">A versão é fixada no momento da criação e não muda com publicações futuras.</p></div>}
        <Select label="Atendimento relacionado (opcional)" value={selectedAppointmentId} onChange={event => setSelectedAppointmentId(event.target.value)}><option value="">Sem atendimento específico</option>{appointments.map(item => <option key={item.id} value={item.id}>{formatClinicDateTime(item.scheduledDateTime)} — {item.procedureName}</option>)}</Select>
        <Button className="w-full" onClick={start} disabled={!selectedFormVersionId || busy}>{busy ? 'Criando rascunho...' : 'Criar rascunho com esta versão'}</Button>
      </div>
    </Dialog>

    {activeSubmission && <SubmissionEditor clientName={clientName} submission={activeSubmission} onClose={() => setActiveSubmission(null)} onChanged={next => { setActiveSubmission(next); void load(); }} onDeleted={() => { setActiveSubmission(null); void load(); }} />}
  </>;
};
