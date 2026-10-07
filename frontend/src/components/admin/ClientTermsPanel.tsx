import React, { useEffect, useRef, useState } from 'react';
import { Download, FileSignature, FileText, LockKeyhole, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import { AvailableTermVersion, TermFieldDefinition, TermInkCapture, TermInkPoint, TermSubmission, TermSubmissionStatus, TermSubmissionSummary } from '../../types';
import { api } from '../../services/api';
import { formatClinicDateTime } from '../../utils/clinicDateTime';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, Dialog, Select, useConfirmationDialog } from '../ui/Components';
import { PdfStage } from './PdfStage';

const statusLabels: Record<TermSubmissionStatus, string> = { Draft: 'Rascunho', Finalized: 'Finalizado', Voided: 'Anulado' };
const statusVariants: Record<TermSubmissionStatus, 'warning' | 'success' | 'danger'> = { Draft: 'warning', Finalized: 'success', Voided: 'danger' };
const signerDeclaration = 'Declaro que li e concordo com as informações e autorizações apresentadas neste termo.';

const normalizePoint = (event: React.PointerEvent<HTMLCanvasElement>): TermInkPoint => {
  const rect = event.currentTarget.getBoundingClientRect();
  return { x: Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width)), y: Math.min(1, Math.max(0, (event.clientY - rect.top) / rect.height)) };
};

const InkPreview: React.FC<{ capture: TermInkCapture }> = ({ capture }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return undefined;
    const redraw = () => {
      const rect = canvas.getBoundingClientRect();
      const ratio = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = Math.max(1, Math.floor(rect.width * ratio));
      canvas.height = Math.max(1, Math.floor(rect.height * ratio));
      const context = canvas.getContext('2d');
      if (!context) return;
      context.clearRect(0, 0, canvas.width, canvas.height);
      context.strokeStyle = '#0f172a';
      context.lineWidth = Math.max(3, 3 * ratio);
      context.lineCap = 'round';
      context.lineJoin = 'round';
      for (const stroke of capture.strokes) for (let index = 1; index < stroke.length; index++) {
        const previous = stroke[index - 1];
        const current = stroke[index];
        context.beginPath();
        context.moveTo(previous.x * canvas.width, previous.y * canvas.height);
        context.lineTo(current.x * canvas.width, current.y * canvas.height);
        context.stroke();
      }
    };
    redraw();
    const observer = new ResizeObserver(redraw);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [capture]);
  return <canvas ref={canvasRef} className="pointer-events-none absolute inset-0 h-full w-full" aria-label="Escrita capturada" />;
};

const InlineInkCanvas: React.FC<{
  field: TermFieldDefinition;
  capture: TermInkCapture;
  style: React.CSSProperties;
  onChange: (capture: TermInkCapture) => void;
  onClear: () => void;
  onCancel: () => void;
  onConfirm: () => void;
  canConfirm: boolean;
  signerName?: string;
  declarationAccepted?: boolean;
  onSignerNameChange?: (value: string) => void;
  onDeclarationAcceptedChange?: (accepted: boolean) => void;
}> = ({ field, capture, style, onChange, onClear, onCancel, onConfirm, canConfirm, signerName, declarationAccepted, onSignerNameChange, onDeclarationAcceptedChange }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const strokesRef = useRef<TermInkPoint[][]>(capture.strokes.map(stroke => [...stroke]));
  const activeRef = useRef<TermInkPoint[] | null>(null);
  const pointerRef = useRef<TermInkCapture['pointerType']>(capture.pointerType);

  const redraw = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = Math.max(1, Math.floor(rect.width * ratio));
    canvas.height = Math.max(1, Math.floor(rect.height * ratio));
    const context = canvas.getContext('2d');
    if (!context) return;
    context.clearRect(0, 0, canvas.width, canvas.height);
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
    strokesRef.current = capture.strokes.map(stroke => [...stroke]);
    pointerRef.current = capture.pointerType;
    redraw();
    const canvas = canvasRef.current;
    if (!canvas) return undefined;
    const observer = new ResizeObserver(redraw);
    observer.observe(canvas);
    return () => observer.disconnect();
  }, [capture]);

  const notify = () => onChange({ ...capture, pointerType: pointerRef.current, strokes: strokesRef.current.map(stroke => [...stroke]) });
  const start = (event: React.PointerEvent<HTMLCanvasElement>) => {
    event.preventDefault();
    event.stopPropagation();
    event.currentTarget.setPointerCapture(event.pointerId);
    pointerRef.current = event.pointerType === 'touch' || event.pointerType === 'pen' || event.pointerType === 'mouse' ? event.pointerType : 'unknown';
    const stroke = [normalizePoint(event)];
    strokesRef.current.push(stroke);
    activeRef.current = stroke;
  };
  const move = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const stroke = activeRef.current;
    if (!stroke) return;
    event.preventDefault();
    const point = normalizePoint(event);
    const previous = stroke[stroke.length - 1];
    stroke.push(point);
    const context = event.currentTarget.getContext('2d');
    if (!context) return;
    context.beginPath();
    context.moveTo(previous.x * event.currentTarget.width, previous.y * event.currentTarget.height);
    context.lineTo(point.x * event.currentTarget.width, point.y * event.currentTarget.height);
    context.stroke();
  };
  const end = (event: React.PointerEvent<HTMLCanvasElement>) => {
    if (activeRef.current && activeRef.current.length < 2) strokesRef.current.pop();
    activeRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    notify();
  };

  const fieldCenter = field.x + field.width / 2;
  const tooltipPosition = fieldCenter < .26 ? 'left-0' : fieldCenter > .74 ? 'right-0' : 'left-1/2 -translate-x-1/2';

  return <div className="absolute z-20 rounded border-2 border-rose-500 bg-transparent shadow-lg" style={style}>
    <canvas ref={canvasRef} className="h-full w-full touch-none cursor-crosshair" onPointerDown={start} onPointerMove={move} onPointerUp={end} onPointerCancel={end} aria-label={`Escrever diretamente em ${field.label}`} />
    <div className={`absolute bottom-full z-30 mb-2 ${tooltipPosition}`} onPointerDown={event => event.stopPropagation()}>
      <div className="flex min-w-max flex-nowrap items-center gap-1 rounded-lg border border-slate-600 bg-slate-950/95 p-1 shadow-xl">
        <Button type="button" size="sm" variant="outline" onClick={onClear} disabled={!capture.strokes.some(stroke => stroke.length >= 2)}><Trash2 className="h-3.5 w-3.5" /> Limpar</Button>
        <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancelar</Button>
        <Button type="button" size="sm" onClick={onConfirm} disabled={!canConfirm}><ShieldCheck className="h-3.5 w-3.5" /> Salvar</Button>
      </div>
      {field.type === 'Signature' && <div className="mt-2 w-72 space-y-2 rounded-lg border border-slate-600 bg-slate-950/95 p-3 text-left shadow-xl"><label className="block text-[11px] font-medium text-slate-200">Nome do signatário<input value={signerName ?? ''} onChange={event => onSignerNameChange?.(event.target.value)} className="mt-1 w-full rounded border border-slate-600 bg-slate-900 px-2 py-1.5 text-xs text-slate-100 outline-none focus:border-rose-500" /></label><label className="flex items-start gap-2 text-[10px] text-slate-300"><input className="mt-0.5" type="checkbox" checked={Boolean(declarationAccepted)} onChange={event => onDeclarationAcceptedChange?.(event.target.checked)} /><span>{signerDeclaration}</span></label></div>}
    </div>
  </div>;
};

const wrapText = (text: string, maxChars: number) => text.split(/\s+/).reduce<string[]>((lines, word) => {
  const current = lines.at(-1) ?? '';
  if (!current || `${current} ${word}`.length <= maxChars) lines[lines.length - 1] = current ? `${current} ${word}` : word;
  else lines.push(word);
  return lines;
}, ['']);

const buildFinalPdf = async (source: ArrayBuffer, submission: TermSubmission, values: Record<string, string | boolean | null>, ink: TermInkCapture[]): Promise<File> => {
  const { PDFDocument, StandardFonts, rgb } = await import('pdf-lib');
  const document = await PDFDocument.load(source.slice(0));
  const font = await document.embedFont(StandardFonts.Helvetica);
  for (const field of submission.layout.fields) {
    const page = document.getPage(field.pageNumber - 1);
    if (!page) throw new Error(`A página ${field.pageNumber} do PDF não existe mais.`);
    const { width, height } = page.getSize();
    const x = field.x * width;
    const top = height * (1 - field.y);
    const areaWidth = field.width * width;
    const areaHeight = field.height * height;
    if (field.type === 'Text') {
      const value = values[field.id];
      if (typeof value !== 'string' || !value.trim()) continue;
      const fontSize = Math.min(13, Math.max(7, areaHeight / (field.multiline ? 4 : 1.7)));
      const lines = field.multiline ? wrapText(value, Math.max(8, Math.floor(areaWidth / (fontSize * .52)))) : [value];
      page.drawText(lines.join('\n'), { x: x + 2, y: top - fontSize - 2, size: fontSize, font, color: rgb(.04, .09, .16), lineHeight: fontSize * 1.2, maxWidth: areaWidth - 4 });
    }
    if (field.type === 'Date') {
      const value = values[field.id];
      if (typeof value === 'string' && value) page.drawText(value.split('-').reverse().join('/'), { x: x + 2, y: top - 12, size: 10, font, color: rgb(.04, .09, .16) });
    }
    if (field.type === 'Checkbox' && values[field.id] === true) page.drawText('X', { x: x + 2, y: top - areaHeight + 2, size: Math.min(areaHeight * .85, 18), font, color: rgb(.04, .09, .16) });
    if (field.type === 'Handwriting' || field.type === 'Signature') {
      const capture = ink.find(item => item.fieldId === field.id);
      if (!capture) continue;
      for (const stroke of capture.strokes) for (let index = 1; index < stroke.length; index++) {
        const previous = stroke[index - 1];
        const current = stroke[index];
        page.drawLine({ start: { x: x + previous.x * areaWidth, y: top - previous.y * areaHeight }, end: { x: x + current.x * areaWidth, y: top - current.y * areaHeight }, thickness: Math.max(.9, Math.min(2, areaHeight / 65)), color: rgb(.04, .09, .16) });
      }
    }
  }
  const bytes = await document.save();
  const finalBytes = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
  return new File([finalBytes], `termo-${submission.id}.pdf`, { type: 'application/pdf' });
};

const TermSubmissionEditor: React.FC<{ clientName: string; submission: TermSubmission; onClose: () => void; onChanged: (submission: TermSubmission) => void; onDeleted: () => void }> = ({ clientName, submission: initialSubmission, onClose, onChanged, onDeleted }) => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const canManage = can('clinical.manage');
  const [submission, setSubmission] = useState(initialSubmission);
  const [values, setValues] = useState(initialSubmission.payload.values);
  const [ink, setInk] = useState(initialSubmission.payload.ink);
  const [pdfBytes, setPdfBytes] = useState<ArrayBuffer | null>(null);
  const [page, setPage] = useState(1);
  const [inkField, setInkField] = useState<TermFieldDefinition | null>(null);
  const [inkDraft, setInkDraft] = useState<TermInkCapture | null>(null);
  const [busy, setBusy] = useState(false);
  const [showVoid, setShowVoid] = useState(false);
  const [voidReason, setVoidReason] = useState('');
  const { confirm, confirmationDialog } = useConfirmationDialog();
  const editable = canManage && submission.status === 'Draft';

  useEffect(() => { void api.getTermVersionPdf(submission.termTemplateId, submission.versionNumber).then(setPdfBytes).catch(error => showToast(error instanceof Error ? error.message : 'Não foi possível abrir o PDF do termo.', 'error')); }, [submission.id]);
  const update = (next: TermSubmission) => { setSubmission(next); setValues(next.payload.values); setInk(next.payload.ink); onChanged(next); };
  const setValue = (fieldId: string, value: string | boolean | null) => setValues(current => { const next = { ...current }; if (value === null || value === '') delete next[fieldId]; else next[fieldId] = value; return next; });
  const closeInkEditor = () => { setInkField(null); setInkDraft(null); };
  const openInkEditor = (field: TermFieldDefinition) => {
    const existing = ink.find(item => item.fieldId === field.id);
    setInkDraft({ fieldId: field.id, signerName: field.type === 'Signature' ? existing?.signerName ?? clientName : undefined, pointerType: existing?.pointerType ?? 'unknown', declarationAccepted: field.type === 'Signature' ? existing?.declarationAccepted ?? false : undefined, strokes: existing?.strokes.map(stroke => [...stroke]) ?? [] });
    setInkField(field);
  };
  const setCapture = (capture: TermInkCapture) => { setInk(current => [...current.filter(item => item.fieldId !== capture.fieldId), capture]); closeInkEditor(); };
  const hasInkDraft = Boolean(inkDraft?.strokes.some(stroke => stroke.length >= 2));
  const canConfirmInkDraft = hasInkDraft && (!inkField || inkField.type !== 'Signature' || Boolean(inkDraft?.declarationAccepted && (inkDraft.signerName?.trim().length ?? 0) >= 2));
  const save = async () => { setBusy(true); try { update(await api.updateClientTermSubmission(submission.clientId, submission.id, submission.revision, values, ink)); showToast('Rascunho do termo salvo.', 'success'); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível salvar o termo.', 'error'); } finally { setBusy(false); } };
  const finalize = async () => { if (!pdfBytes) return; setBusy(true); try { const finalPdf = await buildFinalPdf(pdfBytes, submission, values, ink); update(await api.finalizeClientTermSubmission(submission.clientId, submission.id, submission.revision, values, ink, finalPdf)); showToast('Termo finalizado e PDF imutável salvo no prontuário.', 'success'); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível finalizar o termo.', 'error'); } finally { setBusy(false); } };
  const deleteDraft = async () => { if (!await confirm({ title: 'Excluir rascunho de termo', description: 'Este rascunho será removido da pasta da cliente. Termos finalizados não podem ser excluídos.', confirmLabel: 'Excluir rascunho', variant: 'danger' })) return; setBusy(true); try { await api.deleteClientTermSubmissionDraft(submission.clientId, submission.id, submission.revision); showToast('Rascunho excluído.', 'success'); onDeleted(); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível excluir o rascunho.', 'error'); } finally { setBusy(false); } };
  const voidSubmission = async () => { setBusy(true); try { update(await api.voidClientTermSubmission(submission.clientId, submission.id, submission.revision, voidReason)); setShowVoid(false); showToast('Termo anulado com o PDF preservado.', 'success'); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível anular o termo.', 'error'); } finally { setBusy(false); } };
  const openFinalPdf = async () => { try { const bytes = await api.getClientTermPdf(submission.clientId, submission.id); const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' })); window.open(url, '_blank', 'noopener,noreferrer'); window.setTimeout(() => URL.revokeObjectURL(url), 60_000); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível abrir o PDF final.', 'error'); } };

  return <><Dialog isOpen onClose={busy ? () => undefined : onClose} title={`${submission.termName} — versão ${submission.versionNumber}`} maxWidth="max-w-7xl"><div className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-800 bg-slate-950/50 p-3"><div className="flex items-center gap-2"><Badge variant={statusVariants[submission.status]}>{statusLabels[submission.status]}</Badge><span className="text-xs text-slate-500">Revisão {submission.revision} • PDF {submission.pdfSha256.slice(0, 12)}…</span></div>{submission.finalizedAtUtc && <span className="text-xs text-slate-500">Finalizado em {new Date(submission.finalizedAtUtc).toLocaleString('pt-BR')}</span>}</div>
    {submission.status === 'Voided' && <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-3 text-xs text-rose-100"><strong>Termo anulado:</strong> {submission.voidReason}</div>}
    <PdfStage pdfBytes={pdfBytes} pageNumber={page} onPageChange={nextPage => { setPage(nextPage); closeInkEditor(); }} label="Preencha apenas as áreas marcadas">{() => <>{submission.layout.fields.filter(field => field.pageNumber === page).map(field => {
      const capture = ink.find(item => item.fieldId === field.id);
      const style: React.CSSProperties = { left: `${field.x * 100}%`, top: `${field.y * 100}%`, width: `${field.width * 100}%`, height: `${field.height * 100}%` };
      const label = field.label;
      if (field.type === 'Text') return editable ? <textarea key={field.id} aria-label={label} value={typeof values[field.id] === 'string' ? values[field.id] as string : ''} placeholder={field.placeholder || label} rows={field.multiline ? 3 : 1} onClick={event => event.stopPropagation()} onChange={event => setValue(field.id, event.target.value || null)} className="absolute resize-none overflow-hidden rounded border border-sky-500/80 bg-white/90 px-1 text-[10px] leading-tight text-slate-950 outline-none focus:ring-2 focus:ring-rose-500" style={style} /> : <div key={field.id} className="absolute overflow-hidden rounded border border-transparent bg-white/5 px-1 text-[10px] text-slate-950" style={style}>{String(values[field.id] ?? '')}</div>;
      if (field.type === 'Date') return editable ? <input key={field.id} type="date" aria-label={label} value={typeof values[field.id] === 'string' ? values[field.id] as string : ''} onClick={event => event.stopPropagation()} onChange={event => setValue(field.id, event.target.value || null)} className="native-temporal-inline absolute rounded border border-sky-500/80 bg-white/90 px-1 text-[9px] text-slate-950 outline-none focus:ring-2 focus:ring-rose-500" style={style} /> : <div key={field.id} className="absolute px-1 text-[10px] text-slate-950" style={style}>{typeof values[field.id] === 'string' ? (values[field.id] as string).split('-').reverse().join('/') : ''}</div>;
      if (field.type === 'Checkbox') return <label key={field.id} onClick={event => event.stopPropagation()} className="absolute flex items-center justify-center rounded border border-sky-500/80 bg-white/90" style={style}><input aria-label={label} type="checkbox" disabled={!editable} checked={values[field.id] === true} onChange={event => setValue(field.id, event.target.checked)} /></label>;
      if (inkField?.id === field.id && inkDraft) return <InlineInkCanvas key={field.id} field={field} capture={inkDraft} style={style} onChange={setInkDraft} onClear={() => setInkDraft(current => current ? { ...current, strokes: [] } : current)} onCancel={closeInkEditor} onConfirm={() => { if (inkDraft) setCapture(inkDraft); }} canConfirm={canConfirmInkDraft} signerName={inkDraft.signerName} declarationAccepted={inkDraft.declarationAccepted} onSignerNameChange={signerName => setInkDraft(current => current ? { ...current, signerName } : current)} onDeclarationAcceptedChange={declarationAccepted => setInkDraft(current => current ? { ...current, declarationAccepted } : current)} />;
      return <button key={field.id} type="button" aria-label={label} disabled={!editable} onClick={event => { event.stopPropagation(); openInkEditor(field); }} className={`absolute overflow-hidden rounded border border-dashed ${capture ? 'border-emerald-500 bg-transparent shadow-sm' : 'border-sky-500 bg-sky-500/10'} text-[9px] text-slate-800 disabled:border-transparent`} style={style}>{capture ? <InkPreview capture={capture} /> : <span className="px-1">{editable ? `${field.type === 'Signature' ? 'Assinar' : 'Escrever'}: ${label}` : ''}</span>}</button>;
    })}</>}</PdfStage>
    {showVoid && <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-3"><label className="mb-1.5 block text-xs font-medium text-rose-200">Motivo da anulação</label><textarea rows={3} value={voidReason} onChange={event => setVoidReason(event.target.value)} className="w-full rounded-lg border border-rose-500/20 bg-slate-950/70 px-3 py-2 text-sm" /><div className="mt-3 flex justify-end gap-2"><Button variant="outline" size="sm" onClick={() => setShowVoid(false)} disabled={busy}>Cancelar</Button><Button variant="danger" size="sm" onClick={() => void voidSubmission()} disabled={busy || voidReason.trim().length < 5}>Confirmar anulação</Button></div></div>}
    <div className="flex flex-wrap justify-end gap-2 border-t border-slate-800 pt-4"><Button variant="outline" onClick={onClose} disabled={busy}>Fechar</Button>{submission.status !== 'Draft' && submission.finalPdfContentUrl && <Button variant="outline" onClick={() => void openFinalPdf()}><Download className="h-4 w-4" /> Abrir PDF final</Button>}{editable && <><Button variant="danger" onClick={() => void deleteDraft()} disabled={busy}><Trash2 className="h-4 w-4" /> Excluir rascunho</Button><Button variant="outline" onClick={() => void save()} disabled={busy}>Salvar rascunho</Button><Button onClick={() => void finalize()} disabled={busy || !pdfBytes || Boolean(inkField)}><LockKeyhole className="h-4 w-4" /> Finalizar termo</Button></>}{canManage && submission.status === 'Finalized' && <Button variant="danger" onClick={() => setShowVoid(true)} disabled={busy}>Anular</Button>}</div>
  </div></Dialog>{confirmationDialog}</>;
};

export const ClientTermsPanel: React.FC<{ clientId: string; clientName: string }> = ({ clientId, clientName }) => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const [available, setAvailable] = useState<AvailableTermVersion[]>([]);
  const [submissions, setSubmissions] = useState<TermSubmissionSummary[]>([]);
  const [appointments, setAppointments] = useState<{ id: string; procedureName: string; scheduledDateTime: string }[]>([]);
  const [startOpen, setStartOpen] = useState(false);
  const [selectedVersionId, setSelectedVersionId] = useState('');
  const [selectedAppointmentId, setSelectedAppointmentId] = useState('');
  const [active, setActive] = useState<TermSubmission | null>(null);
  const [busy, setBusy] = useState(false);
  const load = async () => { try { const [terms, items, allAppointments] = await Promise.all([api.getAvailableTerms(), api.getClientTermSubmissions(clientId), api.getAppointments().catch(() => [])]); setAvailable(terms); setSubmissions(items); setAppointments(allAppointments.filter(item => item.clientId === clientId)); if (!selectedVersionId && terms[0]) setSelectedVersionId(terms[0].termVersionId); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível carregar os termos digitais.', 'error'); } };
  useEffect(() => { void load(); }, [clientId]);
  const start = async () => { if (!selectedVersionId) return; setBusy(true); try { const created = await api.createClientTermSubmission(clientId, selectedVersionId, selectedAppointmentId || undefined); setStartOpen(false); setSelectedAppointmentId(''); setActive(created); await load(); showToast('Rascunho de termo criado com a versão publicada atual.', 'success'); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível iniciar o termo.', 'error'); } finally { setBusy(false); } };
  const open = async (id: string) => { setBusy(true); try { setActive(await api.getClientTermSubmission(clientId, id)); } catch (error) { showToast(error instanceof Error ? error.message : 'Não foi possível abrir o termo.', 'error'); } finally { setBusy(false); } };

  return <><Card className="space-y-4"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-3"><div><h3 className="flex items-center gap-2 font-display font-semibold text-rose-400"><FileSignature className="h-4 w-4" /> Termos digitais em PDF</h3><p className="mt-1 text-[11px] text-slate-500">PDF final, versão, campos e assinatura presencial permanecem vinculados ao prontuário.</p></div>{can('clinical.manage') && <Button size="sm" onClick={() => setStartOpen(true)} disabled={!available.length}><Plus className="h-4 w-4" /> Iniciar termo</Button>}</div>{!submissions.length ? <p className="py-4 text-center text-xs text-slate-500">Nenhum termo digital vinculado a este paciente.</p> : <div className="grid gap-3 md:grid-cols-2">{submissions.map(item => <button key={item.id} type="button" onClick={() => void open(item.id)} className="rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-left transition hover:border-rose-500/40 hover:bg-slate-900"><div className="flex items-start justify-between gap-3"><div><strong className="text-sm text-white">{item.termName}</strong><p className="mt-1 text-[11px] text-slate-500">Versão {item.versionNumber} • atualizado em {new Date(item.updatedAtUtc).toLocaleString('pt-BR')}</p></div><Badge variant={statusVariants[item.status]}>{statusLabels[item.status]}</Badge></div><div className="mt-2 flex gap-3 text-[10px] text-slate-500"><span>Revisão {item.revision}</span>{item.isSigned && <span className="text-emerald-400">✓ Assinado</span>}</div></button>)}</div>}</Card><Dialog isOpen={startOpen} onClose={() => setStartOpen(false)} title="Iniciar termo publicado" maxWidth="max-w-2xl"><div className="space-y-4"><Select label="Termo e versão" value={selectedVersionId} onChange={event => setSelectedVersionId(event.target.value)}>{available.map(item => <option key={item.termVersionId} value={item.termVersionId}>{item.name} — versão {item.versionNumber}</option>)}</Select>{available.find(item => item.termVersionId === selectedVersionId) && <div className="rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-xs text-slate-400">{available.find(item => item.termVersionId === selectedVersionId)?.description || 'Sem descrição.'}<p className="mt-1 text-[10px] text-slate-600">A versão do PDF e os campos são fixados no momento da criação.</p></div>}<Select label="Atendimento relacionado (opcional)" value={selectedAppointmentId} onChange={event => setSelectedAppointmentId(event.target.value)}><option value="">Sem atendimento específico</option>{appointments.map(item => <option key={item.id} value={item.id}>{formatClinicDateTime(item.scheduledDateTime)} — {item.procedureName}</option>)}</Select><Button className="w-full" onClick={() => void start()} disabled={!selectedVersionId || busy}>{busy ? 'Criando rascunho...' : 'Criar rascunho com esta versão'}</Button></div></Dialog>{active && <TermSubmissionEditor clientName={clientName} submission={active} onClose={() => setActive(null)} onChanged={next => { setActive(next); void load(); }} onDeleted={() => { setActive(null); void load(); }} />}</>;
};
