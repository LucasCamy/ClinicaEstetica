import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Archive, Check, FilePlus2, FileText, History, PenLine, Plus, Save, Send, Trash2, Upload } from 'lucide-react';
import { TermFieldDefinition, TermFieldType, TermLayoutDefinition, TermTemplateDetail, TermTemplateStatus, TermTemplateSummary } from '../../types';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, Dialog, Input, Select, SkeletonLoader, useConfirmationDialog } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';
import { PdfStage } from './PdfStage';

const fieldTypeLabels: Record<TermFieldType, string> = {
  Text: 'Texto',
  Date: 'Data',
  Checkbox: 'Checkbox',
  Handwriting: 'Escrita à mão',
  Signature: 'Assinatura',
};

const statusLabels: Record<TermTemplateStatus, string> = { Draft: 'Rascunho', Published: 'Publicado', Archived: 'Arquivado' };
const statusVariant = (status: TermTemplateStatus): 'warning' | 'success' | 'default' => status === 'Published' ? 'success' : status === 'Archived' ? 'default' : 'warning';
const emptyLayout = (): TermLayoutDefinition => ({ fields: [] });
const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, Number.isFinite(value) ? value : min));
const formatBytes = (bytes: number) => bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;

const createField = (type: TermFieldType, pageNumber: number, x: number, y: number): TermFieldDefinition => {
  const dimensions = type === 'Checkbox' ? { width: .055, height: .04 } : type === 'Text' ? { width: .32, height: .065 } : { width: .36, height: .14 };
  return {
    id: `field_${Date.now().toString(36)}`,
    type,
    label: fieldTypeLabels[type],
    required: type === 'Signature',
    placeholder: '',
    pageNumber,
    x: clamp(x, 0, 1 - dimensions.width),
    y: clamp(y, 0, 1 - dimensions.height),
    width: dimensions.width,
    height: dimensions.height,
    multiline: false,
  };
};

export const TermManagementView: React.FC = () => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const canManage = can('forms.manage');
  const [templates, setTemplates] = useState<TermTemplateSummary[]>([]);
  const [detail, setDetail] = useState<TermTemplateDetail | null>(null);
  const [draft, setDraft] = useState<{ name: string; description: string; layout: TermLayoutDefinition }>({ name: '', description: '', layout: emptyLayout() });
  const [pdfBytes, setPdfBytes] = useState<ArrayBuffer | null>(null);
  const [selectedPage, setSelectedPage] = useState(1);
  const [selectedFieldId, setSelectedFieldId] = useState<string | null>(null);
  const [placingType, setPlacingType] = useState<TermFieldType | null>(null);
  const [newFieldType, setNewFieldType] = useState<TermFieldType>('Text');
  const [includeArchived, setIncludeArchived] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [createName, setCreateName] = useState('');
  const [createDescription, setCreateDescription] = useState('');
  const [createPdf, setCreatePdf] = useState<File | null>(null);
  const [changeSummary, setChangeSummary] = useState('');
  const { confirm, confirmationDialog } = useConfirmationDialog();
  const movingFieldRef = useRef<{ fieldId: string; pointerId: number; offsetX: number; offsetY: number } | null>(null);
  const resizingFieldRef = useRef<{ fieldId: string; pointerId: number } | null>(null);

  const loadTemplates = async (archived = includeArchived, selectFirst = false) => {
    setIsLoading(true);
    try {
      const items = await api.getTerms(archived);
      setTemplates(items);
      if (selectFirst && items[0]) await openTemplate(items[0].id);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar os termos.', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  const openTemplate = async (id: string) => {
    try {
      const next = await api.getTerm(id);
      setDetail(next);
      setDraft({ name: next.name, description: next.description, layout: next.draftLayout });
      setSelectedFieldId(null);
      setSelectedPage(1);
      setPlacingType(null);
      setPdfBytes(next.hasDraftPdf ? await api.getTermDraftPdf(id) : null);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível abrir o termo.', 'error');
    }
  };

  useEffect(() => { void loadTemplates(false, true); }, []);

  const selectedField = useMemo(() => draft.layout.fields.find(field => field.id === selectedFieldId) ?? null, [draft, selectedFieldId]);
  const hasUnsavedDraftChanges = useMemo(() => detail !== null && (
    detail.name !== draft.name ||
    detail.description !== draft.description ||
    JSON.stringify(detail.draftLayout) !== JSON.stringify(draft.layout)
  ), [detail, draft]);
  const changeField = (id: string, patch: Partial<TermFieldDefinition>) => setDraft(current => ({
    ...current,
    layout: { fields: current.layout.fields.map(field => field.id === id ? { ...field, ...patch } : field) },
  }));

  const uploadPdf = async (file: File) => {
    if (!detail) return;
    setIsSaving(true);
    try {
      const next = await api.uploadTermDraftPdf(detail.id, file, detail.draftRevision);
      setDetail(next);
      setDraft({ name: next.name, description: next.description, layout: next.draftLayout });
      setPdfBytes(await api.getTermDraftPdf(next.id));
      setSelectedPage(1);
      showToast('PDF do termo enviado. Confira e ajuste as áreas de preenchimento.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível enviar o PDF.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const create = async () => {
    if (!createPdf) {
      showToast('Selecione o PDF-base do termo.', 'error');
      return;
    }
    setIsSaving(true);
    try {
      const created = await api.createTerm({ name: createName, description: createDescription });
      const next = await api.uploadTermDraftPdf(created.id, createPdf, created.draftRevision);
      setCreateOpen(false);
      setCreateName('');
      setCreateDescription('');
      setCreatePdf(null);
      await openTemplate(next.id);
      await loadTemplates();
      showToast('Termo criado. Agora delimite os campos de preenchimento.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível criar o termo.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const saveDraft = async () => {
    if (!detail) return;
    setIsSaving(true);
    try {
      const next = await api.updateTermDraft(detail.id, { draftRevision: detail.draftRevision, ...draft });
      setDetail(next);
      setDraft({ name: next.name, description: next.description, layout: next.draftLayout });
      await loadTemplates();
      showToast('Rascunho do termo salvo.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível salvar o rascunho.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const publish = async () => {
    if (!detail) return;
    setIsSaving(true);
    try {
      let preparedDraft = detail;
      if (hasUnsavedDraftChanges) {
        preparedDraft = await api.updateTermDraft(detail.id, { draftRevision: detail.draftRevision, ...draft });
        setDetail(preparedDraft);
        setDraft({ name: preparedDraft.name, description: preparedDraft.description, layout: preparedDraft.draftLayout });
      }
      await api.publishTerm(preparedDraft.id, preparedDraft.draftRevision, changeSummary);
      setPublishOpen(false);
      setChangeSummary('');
      await openTemplate(preparedDraft.id);
      await loadTemplates();
      showToast('Nova versão do termo publicada e congelada.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível publicar o termo.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const archive = async () => {
    if (!detail || !await confirm({
      title: 'Arquivar termo digital',
      description: 'As versões já publicadas e os termos aplicados a clientes serão preservados. Este modelo deixará de estar disponível para novos atendimentos.',
      confirmLabel: 'Arquivar termo',
      variant: 'danger',
    })) return;
    setIsSaving(true);
    try {
      const next = await api.archiveTerm(detail.id);
      setDetail(next);
      setDraft({ name: next.name, description: next.description, layout: next.draftLayout });
      await loadTemplates();
      showToast('Termo arquivado. O histórico foi preservado.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível arquivar o termo.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const addFieldAt = (pageNumber: number, x: number, y: number) => {
    if (!placingType) return;
    const field = createField(placingType, pageNumber, x, y);
    setDraft(current => ({ ...current, layout: { fields: [...current.layout.fields, field] } }));
    setSelectedFieldId(field.id);
    setPlacingType(null);
  };

  const editable = Boolean(canManage && detail?.status !== 'Archived');
  const moveField = (event: React.PointerEvent<HTMLButtonElement>, field: TermFieldDefinition) => {
    const stage = event.currentTarget.parentElement;
    const movement = movingFieldRef.current;
    if (!stage || !movement) return;
    const bounds = stage.getBoundingClientRect();
    if (!bounds.width || !bounds.height) return;
    changeField(field.id, {
      x: clamp((event.clientX - bounds.left) / bounds.width - movement.offsetX, 0, 1 - field.width),
      y: clamp((event.clientY - bounds.top) / bounds.height - movement.offsetY, 0, 1 - field.height),
    });
  };
  const startMove = (event: React.PointerEvent<HTMLButtonElement>, field: TermFieldDefinition) => {
    if (!editable || placingType || event.button !== 0) return;
    const stage = event.currentTarget.parentElement;
    if (!stage) return;
    const bounds = stage.getBoundingClientRect();
    if (!bounds.width || !bounds.height) return;
    event.stopPropagation();
    setSelectedFieldId(field.id);
    movingFieldRef.current = {
      fieldId: field.id,
      pointerId: event.pointerId,
      offsetX: clamp((event.clientX - bounds.left) / bounds.width - field.x, 0, field.width),
      offsetY: clamp((event.clientY - bounds.top) / bounds.height - field.y, 0, field.height),
    };
    event.currentTarget.setPointerCapture(event.pointerId);
  };
  const moveSelectedField = (event: React.PointerEvent<HTMLButtonElement>, field: TermFieldDefinition) => {
    if (movingFieldRef.current?.fieldId !== field.id || movingFieldRef.current.pointerId !== event.pointerId) return;
    event.preventDefault();
    event.stopPropagation();
    moveField(event, field);
  };
  const endMove = (event: React.PointerEvent<HTMLButtonElement>) => {
    if (movingFieldRef.current?.pointerId !== event.pointerId) return;
    movingFieldRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
  };
  const resizeField = (event: React.PointerEvent<HTMLSpanElement>, field: TermFieldDefinition) => {
    const stage = event.currentTarget.parentElement?.parentElement;
    if (!stage) return;
    const bounds = stage.getBoundingClientRect();
    if (!bounds.width || !bounds.height) return;
    const minimum = field.type === 'Checkbox' ? { width: .025, height: .025 } : { width: .06, height: .04 };
    changeField(field.id, {
      width: clamp((event.clientX - bounds.left) / bounds.width - field.x, minimum.width, 1 - field.x),
      height: clamp((event.clientY - bounds.top) / bounds.height - field.y, minimum.height, 1 - field.y),
    });
  };
  const startResize = (event: React.PointerEvent<HTMLSpanElement>, field: TermFieldDefinition) => {
    if (!editable) return;
    event.preventDefault();
    event.stopPropagation();
    resizingFieldRef.current = { fieldId: field.id, pointerId: event.pointerId };
    event.currentTarget.setPointerCapture(event.pointerId);
    resizeField(event, field);
  };
  const moveResize = (event: React.PointerEvent<HTMLSpanElement>, field: TermFieldDefinition) => {
    if (resizingFieldRef.current?.fieldId !== field.id || resizingFieldRef.current.pointerId !== event.pointerId) return;
    event.preventDefault();
    event.stopPropagation();
    resizeField(event, field);
  };
  const endResize = (event: React.PointerEvent<HTMLSpanElement>) => {
    if (resizingFieldRef.current?.pointerId !== event.pointerId) return;
    resizingFieldRef.current = null;
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
  };

  return <div className="space-y-6">
    <AdminPageHeader eyebrow="Documentos clínicos" title="Termos digitais em PDF" description="Envie o PDF, delimite campos de preenchimento e publique versões imutáveis para usar no atendimento." actions={canManage ? <Button onClick={() => setCreateOpen(true)}><FilePlus2 className="h-4 w-4" /> Novo termo PDF</Button> : undefined} />

    <div className="rounded-xl border border-amber-500/20 bg-amber-500/5 px-4 py-3 text-xs text-amber-100">O PDF-base não é alterado. Os textos e traços serão incorporados somente no PDF final do cliente, junto com a versão exata do termo.</div>

    <div className="grid gap-6 xl:grid-cols-[300px_minmax(0,1fr)]">
      <aside className="space-y-6 xl:sticky xl:top-6 xl:max-h-[calc(100vh-3rem)] xl:self-start xl:overflow-y-auto xl:pr-1">
        <Card className="space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3"><h2 className="font-display font-semibold text-slate-100">Catálogo</h2><label className="flex items-center gap-2 text-[11px] text-slate-400"><input type="checkbox" checked={includeArchived} onChange={event => { setIncludeArchived(event.target.checked); void loadTemplates(event.target.checked); }} /> Arquivados</label></div>
          {isLoading ? <SkeletonLoader className="h-28" /> : !templates.length ? <p className="py-6 text-center text-xs text-slate-500">Nenhum termo criado.</p> : <div className="max-h-[52vh] space-y-2 overflow-y-auto pr-1">{templates.map(template => <button key={template.id} type="button" onClick={() => void openTemplate(template.id)} className={`w-full rounded-lg border p-3 text-left ${detail?.id === template.id ? 'border-rose-500/50 bg-rose-500/10' : 'border-slate-800 bg-slate-900/60 hover:border-slate-700'}`}><div className="flex items-start justify-between gap-2"><strong className="text-sm text-slate-100">{template.name}</strong><Badge variant={statusVariant(template.status)}>{statusLabels[template.status]}</Badge></div><p className="mt-1 text-[11px] text-slate-500">{template.hasDraftPdf ? `${template.draftFieldCount} campo(s)` : 'PDF pendente'}</p>{template.hasUnpublishedChanges && template.latestPublishedVersionNumber && <p className="mt-1 text-[10px] text-amber-400">Alterações não publicadas</p>}</button>)}</div>}
        </Card>
        {detail && <Card className="space-y-3"><h2 className="flex items-center gap-2 border-b border-slate-800 pb-3 font-display font-semibold text-rose-400"><History className="h-4 w-4" /> Versões publicadas</h2>{!detail.versions.length ? <p className="text-xs text-slate-500">Nenhuma versão publicada.</p> : detail.versions.map(version => <div key={version.id} className="rounded-lg border border-slate-800 bg-slate-900/60 p-3"><div className="flex items-center justify-between text-xs"><strong className="text-slate-200">Versão {version.versionNumber}</strong><span className="text-slate-500">{formatBytes(version.pdfFileSizeBytes)}</span></div><p className="mt-1 text-[11px] text-slate-400">{version.changeSummary}</p><p className="mt-1 text-[10px] text-slate-600">{new Date(version.publishedAtUtc).toLocaleString('pt-BR')}</p></div>)}</Card>}
      </aside>

      {!detail ? <Card className="flex min-h-80 flex-col items-center justify-center text-center"><FileText className="mb-3 h-10 w-10 text-slate-700" /><h2 className="font-display text-lg font-semibold text-slate-300">Crie ou selecione um termo</h2><p className="mt-1 max-w-md text-xs text-slate-500">O PDF e os campos ficam no rascunho até a publicação de uma versão.</p></Card> : <div className="space-y-6">
        <Card className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-4"><div className="flex items-center gap-2"><Badge variant={statusVariant(detail.status)}>{statusLabels[detail.status]}</Badge><span className="text-xs text-slate-500">Revisão do rascunho {detail.draftRevision}{hasUnsavedDraftChanges ? ' • alterações pendentes' : ''}</span></div><div className="flex flex-wrap gap-2">{editable && <><Button size="sm" variant="outline" onClick={archive} disabled={isSaving}><Archive className="h-3.5 w-3.5" /> Arquivar</Button><Button size="sm" variant="outline" onClick={() => void saveDraft()} disabled={isSaving}><Save className="h-3.5 w-3.5" /> Salvar rascunho</Button><Button size="sm" onClick={() => setPublishOpen(true)} disabled={isSaving || !detail.hasDraftPdf || draft.layout.fields.length === 0}><Send className="h-3.5 w-3.5" /> {hasUnsavedDraftChanges ? 'Salvar e publicar' : 'Publicar versão'}</Button></>}</div></div>
          <div className="grid gap-4 md:grid-cols-2"><Input label="Nome do termo" disabled={!editable} value={draft.name} onChange={event => setDraft({ ...draft, name: event.target.value })} /><Input label="Descrição" disabled={!editable} value={draft.description} onChange={event => setDraft({ ...draft, description: event.target.value })} /></div>
          <div className="rounded-xl border border-slate-800 bg-slate-950/40 p-3"><div className="flex flex-wrap items-center justify-between gap-3"><div><strong className="text-sm text-slate-200">PDF-base</strong><p className="mt-1 text-[11px] text-slate-500">{detail.hasDraftPdf ? `${detail.draftPdfFileName} • ${formatBytes(detail.draftPdfFileSizeBytes)}` : 'Nenhum PDF enviado.'}</p></div>{editable && <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg border border-slate-700 px-3 py-2 text-xs text-slate-200 hover:bg-slate-800"><Upload className="h-4 w-4" /> Trocar PDF<input type="file" accept="application/pdf,.pdf" className="sr-only" onChange={event => { const file = event.target.files?.[0]; if (file) void uploadPdf(file); event.currentTarget.value = ''; }} /></label>}</div></div>
        </Card>

        {detail.hasDraftPdf ? <div className="grid min-w-0 gap-6 2xl:grid-cols-[minmax(0,1fr)_20rem]">
          <Card className="min-w-0 space-y-4"><div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-4"><div><h2 className="font-display font-semibold text-rose-400">Mapa de campos</h2><p className="text-[11px] text-slate-500">Arraste um campo para reposicioná-lo. Use o canto inferior direito para mudar o tamanho.</p></div>{editable && <div className="flex w-full gap-2 sm:w-auto"><div className="min-w-0 flex-1 sm:w-48 sm:flex-none"><Select value={newFieldType} onChange={event => setNewFieldType(event.target.value as TermFieldType)}>{(Object.keys(fieldTypeLabels) as TermFieldType[]).map(type => <option key={type} value={type}>{fieldTypeLabels[type]}</option>)}</Select></div><Button size="sm" variant={placingType ? 'outline' : 'primary'} onClick={() => setPlacingType(placingType ? null : newFieldType)}><Plus className="h-4 w-4" /> {placingType ? 'Cancelar' : 'Adicionar'}</Button></div>}</div>
            <PdfStage pdfBytes={pdfBytes} pageNumber={selectedPage} onPageChange={setSelectedPage} onPageClick={placingType ? addFieldAt : undefined} label={placingType ? 'Clique para inserir o campo selecionado' : 'PDF-base do termo'}>{() => <>{draft.layout.fields.filter(field => field.pageNumber === selectedPage).map(field => <button key={field.id} type="button" onClick={event => { event.stopPropagation(); setSelectedFieldId(field.id); }} onPointerDown={event => startMove(event, field)} onPointerMove={event => moveSelectedField(event, field)} onPointerUp={endMove} onPointerCancel={endMove} onLostPointerCapture={endMove} className={`absolute touch-none overflow-hidden rounded border text-left text-[9px] font-semibold shadow-sm ${editable && !placingType ? 'cursor-move' : ''} ${selectedFieldId === field.id ? 'border-rose-400 bg-rose-500/25 text-white ring-2 ring-rose-500/50' : 'border-sky-400/80 bg-sky-500/15 text-sky-950 hover:bg-sky-500/25'}`} style={{ left: `${field.x * 100}%`, top: `${field.y * 100}%`, width: `${field.width * 100}%`, height: `${field.height * 100}%` }}><span className="block truncate px-1">{field.label}</span>{editable && <span aria-hidden="true" title="Arraste para redimensionar" onClick={event => event.stopPropagation()} onPointerDown={event => startResize(event, field)} onPointerMove={event => moveResize(event, field)} onPointerUp={endResize} onPointerCancel={endResize} onLostPointerCapture={endResize} className="absolute bottom-0 right-0 flex h-4 w-4 touch-none cursor-nwse-resize items-end justify-end border-l border-t border-current/70 bg-slate-950/45 pr-px text-[10px] leading-none">↘</span>}</button>)}</>}</PdfStage>
          </Card>
          <Card className="space-y-4 2xl:sticky 2xl:top-6 2xl:self-start"><h2 className="flex items-center gap-2 border-b border-slate-800 pb-3 font-display font-semibold text-rose-400"><PenLine className="h-4 w-4" /> Propriedades</h2>{!selectedField ? <p className="py-8 text-center text-xs text-slate-500">Clique em um campo do PDF para configurá-lo.</p> : <><Input label="Título do campo" disabled={!editable} value={selectedField.label} onChange={event => changeField(selectedField.id, { label: event.target.value })} /><Select label="Tipo" disabled={!editable} value={selectedField.type} onChange={event => changeField(selectedField.id, { type: event.target.value as TermFieldType, multiline: false })}>{(Object.keys(fieldTypeLabels) as TermFieldType[]).map(type => <option key={type} value={type}>{fieldTypeLabels[type]}</option>)}</Select>{selectedField.type === 'Text' && <><Input label="Texto auxiliar" disabled={!editable} value={selectedField.placeholder} onChange={event => changeField(selectedField.id, { placeholder: event.target.value })} /><label className="flex items-center gap-2 text-xs text-slate-300"><input type="checkbox" disabled={!editable} checked={selectedField.multiline} onChange={event => changeField(selectedField.id, { multiline: event.target.checked })} /> Permitir mais de uma linha</label></>}<label className="flex items-center gap-2 text-xs text-slate-300"><input type="checkbox" disabled={!editable} checked={selectedField.required} onChange={event => changeField(selectedField.id, { required: event.target.checked })} /> Preenchimento obrigatório</label><div className="grid grid-cols-2 gap-3">{(['x', 'y', 'width', 'height'] as const).map(key => <Input key={key} label={key === 'x' ? 'Posição X' : key === 'y' ? 'Posição Y' : key === 'width' ? 'Largura' : 'Altura'} disabled={!editable} type="number" min="0" max="1" step="0.01" value={selectedField[key]} onChange={event => changeField(selectedField.id, { [key]: Number(event.target.value) } as Partial<TermFieldDefinition>)} />)}</div><p className="text-[10px] text-slate-500">As medidas usam a proporção da página: 0 é o início e 1 é o fim.</p>{editable && <Button variant="danger" size="sm" className="w-full" onClick={() => { setDraft(current => ({ ...current, layout: { fields: current.layout.fields.filter(field => field.id !== selectedField.id) } })); setSelectedFieldId(null); }}><Trash2 className="h-4 w-4" /> Remover campo</Button>}</>}</Card>
        </div> : <Card className="py-14 text-center"><Upload className="mx-auto h-8 w-8 text-slate-600" /><p className="mt-3 text-sm text-slate-400">Envie o PDF-base para começar a configurar o termo.</p></Card>}
      </div>}
    </div>

    <Dialog isOpen={createOpen} onClose={() => !isSaving && setCreateOpen(false)} title="Criar termo digital em PDF" maxWidth="max-w-xl"><div className="space-y-4"><Input label="Nome do termo" required value={createName} onChange={event => setCreateName(event.target.value)} placeholder="Ex.: Termo de consentimento facial" /><Input label="Descrição" value={createDescription} onChange={event => setCreateDescription(event.target.value)} placeholder="Uso e finalidade do termo" /><div className="space-y-1.5"><label className="block text-xs font-medium text-slate-300">PDF-base</label><input type="file" accept="application/pdf,.pdf" onChange={event => setCreatePdf(event.target.files?.[0] ?? null)} className="block w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3 py-2 text-sm text-slate-200 file:mr-3 file:rounded file:border-0 file:bg-slate-800 file:px-2 file:py-1 file:text-xs file:text-slate-100" />{createPdf && <p className="text-[11px] text-slate-500">{createPdf.name} • {formatBytes(createPdf.size)}</p>}</div><Button className="w-full" disabled={isSaving || createName.trim().length < 2 || !createPdf} onClick={() => void create()}>{isSaving ? 'Criando...' : 'Criar e abrir editor'}</Button></div></Dialog>
    <Dialog isOpen={publishOpen} onClose={() => !isSaving && setPublishOpen(false)} title="Publicar nova versão do termo" maxWidth="max-w-xl"><div className="space-y-4"><p className="text-sm text-slate-400">A versão publicada congela o PDF e todas as áreas configuradas. Alterações futuras criarão outra versão.</p><Input label="Resumo das alterações" required minLength={3} maxLength={500} value={changeSummary} onChange={event => setChangeSummary(event.target.value)} /><Button className="w-full" disabled={isSaving || changeSummary.trim().length < 3} onClick={() => void publish()}><Check className="h-4 w-4" /> Confirmar publicação</Button></div></Dialog>
    {confirmationDialog}
  </div>;
};
