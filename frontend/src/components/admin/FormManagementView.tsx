import React, { useEffect, useState } from 'react';
import {
  Archive, ArrowDown, ArrowUp, ClipboardList, Copy, Eye, FilePlus2,
  History, Plus, Save, Send, Trash2
} from 'lucide-react';
import {
  FormFieldDefinition, FormFieldOption, FormFieldType, FormSchemaDefinition,
  FormTemplateDetail, FormTemplateSummary, FormVersion
} from '../../types';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, Dialog, Input, Select, SkeletonLoader, useConfirmationDialog } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';

const fieldTypeLabels: Record<FormFieldType, string> = {
  Section: 'Seção / título',
  InformationalText: 'Texto informativo',
  ShortText: 'Texto curto',
  LongText: 'Texto longo',
  Number: 'Número',
  Date: 'Data',
  YesNo: 'Sim ou não',
  Checkbox: 'Caixa de confirmação',
  CheckboxGroup: 'Grupo de checkboxes',
  Dropdown: 'Lista suspensa',
  MultiSelect: 'Seleção múltipla',
  Signature: 'Assinatura presencial',
};

const fieldTypes = Object.keys(fieldTypeLabels) as FormFieldType[];
const optionFieldTypes: FormFieldType[] = ['CheckboxGroup', 'Dropdown', 'MultiSelect'];
const displayFieldTypes: FormFieldType[] = ['Section', 'InformationalText'];

const stableId = (prefix: 'field' | 'opt') =>
  `${prefix}_${crypto.randomUUID().replaceAll('-', '')}`;

const defaultOptions = (): FormFieldOption[] => [
  { id: stableId('opt'), label: 'Opção 1' },
  { id: stableId('opt'), label: 'Opção 2' },
];

const createField = (type: FormFieldType): FormFieldDefinition => ({
  id: stableId('field'),
  type,
  label: type === 'Section' ? 'Nova seção' : type === 'InformationalText' ? 'Texto de orientação' : 'Novo campo',
  description: '',
  required: false,
  placeholder: '',
  options: optionFieldTypes.includes(type) ? defaultOptions() : [],
});

const statusBadge = (status: FormTemplateSummary['status']) => {
  if (status === 'Published') return <Badge variant="success">Publicado</Badge>;
  if (status === 'Archived') return <Badge variant="default">Arquivado</Badge>;
  return <Badge variant="warning">Rascunho</Badge>;
};

const FormPreview: React.FC<{ schema: FormSchemaDefinition }> = ({ schema }) => (
  <div className="space-y-4">
    {schema.fields.length === 0 && (
      <p className="rounded-lg border border-dashed border-slate-700 p-6 text-center text-xs text-slate-500">
        Adicione campos para visualizar o formulário.
      </p>
    )}
    {schema.fields.map(field => {
      if (field.type === 'Section') {
        return <h4 key={field.id} className="border-b border-slate-700 pb-2 font-display text-base font-semibold text-rose-400">{field.label}</h4>;
      }
      if (field.type === 'InformationalText') {
        return <p key={field.id} className="rounded-lg bg-slate-900/70 p-3 text-xs leading-relaxed text-slate-300">{field.label}{field.description && ` — ${field.description}`}</p>;
      }

      const label = `${field.label}${field.required ? ' *' : ''}`;
      return (
        <div key={field.id} className="space-y-1.5">
          <span className="block text-xs font-medium text-slate-300">{label}</span>
          {field.description && <p className="text-[11px] text-slate-500">{field.description}</p>}
          {field.type === 'LongText' && <textarea disabled rows={3} placeholder={field.placeholder} className="w-full rounded-lg border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm" />}
          {['ShortText', 'Number', 'Date'].includes(field.type) && <input disabled type={field.type === 'Number' ? 'number' : field.type === 'Date' ? 'date' : 'text'} placeholder={field.placeholder} className="w-full rounded-lg border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm" />}
          {field.type === 'Dropdown' && <select disabled className="w-full rounded-lg border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm"><option>Selecione...</option>{field.options.map(option => <option key={option.id}>{option.label}</option>)}</select>}
          {field.type === 'YesNo' && <div className="flex gap-4 text-xs text-slate-400"><label><input type="radio" disabled /> Sim</label><label><input type="radio" disabled /> Não</label></div>}
          {field.type === 'Checkbox' && <label className="flex items-center gap-2 text-xs text-slate-400"><input type="checkbox" disabled /> Confirmar</label>}
          {['CheckboxGroup', 'MultiSelect'].includes(field.type) && <div className="grid gap-2 sm:grid-cols-2">{field.options.map(option => <label key={option.id} className="flex items-center gap-2 text-xs text-slate-400"><input type="checkbox" disabled /> {option.label}</label>)}</div>}
          {field.type === 'Signature' && <div className="flex h-28 items-center justify-center rounded-lg border-2 border-dashed border-slate-700 text-xs text-slate-500">Área de assinatura presencial — habilitada na etapa de preenchimento</div>}
        </div>
      );
    })}
  </div>
);

export const FormManagementView: React.FC = () => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const canManage = can('forms.manage');
  const [templates, setTemplates] = useState<FormTemplateSummary[]>([]);
  const [detail, setDetail] = useState<FormTemplateDetail | null>(null);
  const [draft, setDraft] = useState({ name: '', category: '', description: '', schema: { fields: [] } as FormSchemaDefinition });
  const [isDirty, setIsDirty] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [newFieldType, setNewFieldType] = useState<FormFieldType>('ShortText');
  const [createOpen, setCreateOpen] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [duplicateOpen, setDuplicateOpen] = useState(false);
  const [versionOpen, setVersionOpen] = useState(false);
  const [selectedVersion, setSelectedVersion] = useState<FormVersion | null>(null);
  const [createData, setCreateData] = useState({ name: '', category: 'Anamnese', description: '' });
  const [changeSummary, setChangeSummary] = useState('');
  const [duplicateName, setDuplicateName] = useState('');
  const { confirm, confirmationDialog } = useConfirmationDialog();

  const loadTemplates = async (archived = includeArchived) => {
    setIsLoading(true);
    try {
      setTemplates(await api.getForms(archived));
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao carregar formulários.', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  const applyDetail = (value: FormTemplateDetail) => {
    setDetail(value);
    setDraft({
      name: value.name,
      category: value.category,
      description: value.description,
      schema: { fields: value.draftSchema.fields.map(field => ({ ...field, options: field.options.map(option => ({ ...option })) })) },
    });
    setIsDirty(false);
  };

  const openTemplate = async (id: string) => {
    if (isDirty && !await confirm({
      title: 'Descartar alterações locais?',
      description: 'As alterações ainda não salvas deste formulário serão perdidas ao abrir outro item.',
      confirmLabel: 'Descartar e abrir',
      variant: 'danger',
    })) return;
    try {
      applyDetail(await api.getForm(id));
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao abrir o formulário.', 'error');
    }
  };

  useEffect(() => { void loadTemplates(); }, []);

  const changeDraft = (next: typeof draft) => {
    setDraft(next);
    setIsDirty(true);
  };

  const updateField = (id: string, changes: Partial<FormFieldDefinition>) => {
    changeDraft({
      ...draft,
      schema: { fields: draft.schema.fields.map(field => field.id === id ? { ...field, ...changes } : field) },
    });
  };

  const changeFieldType = (field: FormFieldDefinition, type: FormFieldType) => {
    updateField(field.id, {
      type,
      required: displayFieldTypes.includes(type) ? false : field.required,
      options: optionFieldTypes.includes(type) ? (field.options.length ? field.options : defaultOptions()) : [],
    });
  };

  const moveField = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= draft.schema.fields.length) return;
    const fields = [...draft.schema.fields];
    [fields[index], fields[target]] = [fields[target], fields[index]];
    changeDraft({ ...draft, schema: { fields } });
  };

  const duplicateField = (field: FormFieldDefinition, index: number) => {
    const copy: FormFieldDefinition = {
      ...field,
      id: stableId('field'),
      label: `${field.label} (cópia)`,
      options: field.options.map(option => ({ ...option, id: stableId('opt') })),
    };
    const fields = [...draft.schema.fields];
    fields.splice(index + 1, 0, copy);
    changeDraft({ ...draft, schema: { fields } });
  };

  const updateOptions = (field: FormFieldDefinition, value: string) => {
    const labels = value.split('\n').map(label => label.trim()).filter(Boolean);
    updateField(field.id, {
      options: labels.map((label, index) => ({ id: field.options[index]?.id || stableId('opt'), label })),
    });
  };

  const saveDraft = async () => {
    if (!detail || !canManage) return;
    setIsSaving(true);
    try {
      const saved = await api.updateFormDraft(detail.id, {
        draftRevision: detail.draftRevision,
        name: draft.name,
        category: draft.category,
        description: draft.description,
        schema: draft.schema,
      });
      applyDetail(saved);
      await loadTemplates();
      showToast('Rascunho salvo com segurança.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao salvar o rascunho.', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const createTemplate = async (event: React.FormEvent) => {
    event.preventDefault();
    try {
      const created = await api.createForm(createData);
      setCreateOpen(false);
      setCreateData({ name: '', category: 'Anamnese', description: '' });
      applyDetail(created);
      await loadTemplates();
      showToast('Novo formulário criado como rascunho.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao criar o formulário.', 'error');
    }
  };

  const publish = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!detail || isDirty) return;
    try {
      const version = await api.publishForm(detail.id, detail.draftRevision, changeSummary);
      setPublishOpen(false);
      setChangeSummary('');
      applyDetail(await api.getForm(detail.id));
      await loadTemplates();
      showToast(`Versão ${version.versionNumber} publicada e congelada.`, 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao publicar o formulário.', 'error');
    }
  };

  const duplicate = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!detail) return;
    try {
      const duplicated = await api.duplicateForm(detail.id, duplicateName);
      setDuplicateOpen(false);
      setDuplicateName('');
      applyDetail(duplicated);
      await loadTemplates();
      showToast('Cópia criada como novo rascunho.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao duplicar o formulário.', 'error');
    }
  };

  const archive = async () => {
    if (!detail || !await confirm({
      title: 'Arquivar formulário',
      description: 'As versões publicadas e os formulários já aplicados serão preservados. O modelo deixará de estar disponível para novos preenchimentos.',
      confirmLabel: 'Arquivar formulário',
      variant: 'danger',
    })) return;
    try {
      applyDetail(await api.archiveForm(detail.id));
      setIncludeArchived(true);
      await loadTemplates(true);
      showToast('Formulário arquivado. O histórico foi preservado.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao arquivar o formulário.', 'error');
    }
  };

  const viewVersion = async (versionNumber: number) => {
    if (!detail) return;
    try {
      setSelectedVersion(await api.getFormVersion(detail.id, versionNumber));
      setVersionOpen(true);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao carregar a versão.', 'error');
    }
  };

  const openCreate = async () => {
    if (isDirty && !await confirm({
      title: 'Descartar alterações locais?',
      description: 'As alterações ainda não salvas deste formulário serão perdidas para criar um novo rascunho.',
      confirmLabel: 'Descartar e criar',
      variant: 'danger',
    })) return;
    setCreateOpen(true);
  };

  const archived = detail?.status === 'Archived';
  const editable = canManage && !archived;

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Formulários clínicos" description="Crie questionários reutilizáveis e publique versões que não podem ser alteradas retroativamente." actions={canManage ? <Button onClick={() => void openCreate()}><FilePlus2 className="h-4 w-4" /> Novo formulário</Button> : undefined} />

      <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 px-4 py-3 text-xs text-amber-200">
        Esta etapa gerencia modelos e versões. O preenchimento no prontuário e a assinatura presencial serão habilitados na próxima integração clínica.
      </div>

      <div className="grid gap-6 xl:grid-cols-[320px_minmax(0,1fr)]">
        <aside className="space-y-6 xl:sticky xl:top-6 xl:max-h-[calc(100vh-3rem)] xl:self-start xl:overflow-y-auto xl:pr-1">
          <Card className="space-y-4">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3">
              <h2 className="font-display font-semibold text-slate-100">Catálogo</h2>
              <label className="flex items-center gap-2 text-[11px] text-slate-400">
                <input type="checkbox" checked={includeArchived} onChange={event => { setIncludeArchived(event.target.checked); void loadTemplates(event.target.checked); }} /> Arquivados
              </label>
            </div>
            {isLoading ? <SkeletonLoader className="h-28" /> : templates.length === 0 ? (
              <p className="py-6 text-center text-xs text-slate-500">Nenhum formulário criado.</p>
            ) : (
              <div className="max-h-[48vh] space-y-2 overflow-y-auto pr-1">
                {templates.map(template => (
                  <button
                    key={template.id}
                    type="button"
                    onClick={() => void openTemplate(template.id)}
                    className={`w-full rounded-lg border p-3 text-left transition-colors ${detail?.id === template.id ? 'border-rose-500/50 bg-rose-500/10' : 'border-slate-800 bg-slate-900/60 hover:border-slate-700'}`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <span className="text-sm font-semibold text-slate-100">{template.name}</span>
                      {statusBadge(template.status)}
                    </div>
                    <p className="mt-1 text-[11px] text-slate-500">{template.category} • {template.draftFieldCount} campo(s)</p>
                    {template.hasUnpublishedChanges && template.latestPublishedVersionNumber && <p className="mt-1 text-[10px] text-amber-400">Alterações ainda não publicadas</p>}
                  </button>
                ))}
              </div>
            )}
          </Card>

          {detail && <Card className="space-y-3">
            <h2 className="flex items-center gap-2 border-b border-slate-800 pb-3 font-display font-semibold text-rose-400"><History className="h-4 w-4" /> Versões publicadas</h2>
            {detail.versions.length === 0 ? <p className="text-xs text-slate-500">Nenhuma versão publicada.</p> : <div className="max-h-[35vh] space-y-2 overflow-y-auto pr-1">{detail.versions.map(version => (
              <button key={version.id} type="button" onClick={() => void viewVersion(version.versionNumber)} className="w-full rounded-lg border border-slate-800 bg-slate-900/60 p-3 text-left hover:border-slate-700">
                <div className="flex items-center justify-between"><span className="text-xs font-semibold text-slate-200">Versão {version.versionNumber}</span><span className="text-[10px] text-slate-500">{new Date(version.publishedAtUtc).toLocaleString('pt-BR')}</span></div>
                <p className="mt-1 text-[11px] text-slate-400">{version.changeSummary}</p>
                <p className="mt-1 text-[10px] text-slate-600">{version.fieldCount} campo(s)</p>
              </button>
            ))}</div>}
          </Card>}
        </aside>

        {!detail ? (
          <Card className="flex min-h-80 flex-col items-center justify-center text-center">
            <ClipboardList className="mb-3 h-10 w-10 text-slate-700" />
            <h2 className="font-display text-lg font-semibold text-slate-300">Selecione ou crie um formulário</h2>
            <p className="mt-1 max-w-md text-xs text-slate-500">O construtor preserva identificadores estáveis dos campos e separa rascunhos de versões publicadas.</p>
          </Card>
        ) : (
          <div className="space-y-6">
            <Card className="space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-800 pb-4">
                <div className="flex items-center gap-2">{statusBadge(detail.status)}<span className="text-xs text-slate-500">Revisão do rascunho {detail.draftRevision}</span></div>
                <div className="flex flex-wrap gap-2">
                  {canManage && <Button variant="outline" size="sm" disabled={isDirty} onClick={() => { setDuplicateName(`${draft.name} — cópia`); setDuplicateOpen(true); }}><Copy className="h-3.5 w-3.5" /> Duplicar</Button>}
                  {editable && <Button variant="outline" size="sm" disabled={isDirty} onClick={() => void archive()}><Archive className="h-3.5 w-3.5" /> Arquivar</Button>}
                  {editable && <Button variant="outline" size="sm" onClick={() => void saveDraft()} disabled={!isDirty || isSaving}><Save className="h-3.5 w-3.5" /> {isSaving ? 'Salvando...' : 'Salvar rascunho'}</Button>}
                  {editable && <Button size="sm" onClick={() => setPublishOpen(true)} disabled={isDirty || !detail.hasUnpublishedChanges || draft.schema.fields.every(field => displayFieldTypes.includes(field.type))}><Send className="h-3.5 w-3.5" /> Publicar versão</Button>}
                </div>
              </div>
              {isDirty && <p className="rounded-md bg-amber-500/10 px-3 py-2 text-xs text-amber-300">Há alterações locais não salvas. Salve o rascunho antes de publicar.</p>}
              {archived && <p className="rounded-md bg-slate-800 px-3 py-2 text-xs text-slate-400">Este formulário está arquivado e permanece somente para consulta histórica.</p>}
              <div className="grid gap-4 md:grid-cols-2">
                <Input label="Nome do formulário" value={draft.name} disabled={!editable} onChange={event => changeDraft({ ...draft, name: event.target.value })} />
                <Input label="Categoria" value={draft.category} disabled={!editable} onChange={event => changeDraft({ ...draft, category: event.target.value })} />
              </div>
              <div className="space-y-1.5">
                <label className="block text-xs font-medium text-slate-300">Descrição e finalidade</label>
                <textarea rows={2} disabled={!editable} value={draft.description} onChange={event => changeDraft({ ...draft, description: event.target.value })} className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm text-slate-100 focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50 disabled:opacity-60" />
              </div>
            </Card>

            <div className="grid gap-6 2xl:grid-cols-[minmax(0,1.25fr)_minmax(320px,.75fr)]">
              <Card className="space-y-4">
                <div className="flex flex-wrap items-end justify-between gap-3 border-b border-slate-800 pb-4">
                  <div><h2 className="font-display font-semibold text-rose-400">Construtor de campos</h2><p className="text-[11px] text-slate-500">Máximo de 100 campos por formulário.</p></div>
                  {editable && <div className="flex gap-2"><Select value={newFieldType} onChange={event => setNewFieldType(event.target.value as FormFieldType)}>{fieldTypes.map(type => <option key={type} value={type}>{fieldTypeLabels[type]}</option>)}</Select><Button size="sm" onClick={() => changeDraft({ ...draft, schema: { fields: [...draft.schema.fields, createField(newFieldType)] } })}><Plus className="h-4 w-4" /> Adicionar</Button></div>}
                </div>
                {draft.schema.fields.length === 0 ? <p className="py-10 text-center text-xs text-slate-500">O rascunho ainda não possui campos.</p> : (
                  <div className="space-y-3">
                    {draft.schema.fields.map((field, index) => (
                      <div key={field.id} className="space-y-3 rounded-xl border border-slate-800 bg-slate-900/60 p-4">
                        <div className="flex items-center justify-between gap-3">
                          <div><span className="text-xs font-semibold text-slate-200">Campo {index + 1}</span><span className="ml-2 font-mono text-[9px] text-slate-600">{field.id}</span></div>
                          {editable && <div className="flex gap-1">
                            <button type="button" aria-label="Mover campo para cima" disabled={index === 0} onClick={() => moveField(index, -1)} className="rounded p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white disabled:opacity-30"><ArrowUp className="h-3.5 w-3.5" /></button>
                            <button type="button" aria-label="Mover campo para baixo" disabled={index === draft.schema.fields.length - 1} onClick={() => moveField(index, 1)} className="rounded p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white disabled:opacity-30"><ArrowDown className="h-3.5 w-3.5" /></button>
                            <button type="button" aria-label="Duplicar campo" onClick={() => duplicateField(field, index)} className="rounded p-1.5 text-slate-400 hover:bg-slate-800 hover:text-white"><Copy className="h-3.5 w-3.5" /></button>
                            <button type="button" aria-label="Remover campo" onClick={() => changeDraft({ ...draft, schema: { fields: draft.schema.fields.filter(item => item.id !== field.id) } })} className="rounded p-1.5 text-rose-400 hover:bg-rose-500/10"><Trash2 className="h-3.5 w-3.5" /></button>
                          </div>}
                        </div>
                        <div className="grid gap-3 md:grid-cols-2">
                          <Select label="Tipo" value={field.type} disabled={!editable} onChange={event => changeFieldType(field, event.target.value as FormFieldType)}>{fieldTypes.map(type => <option key={type} value={type}>{fieldTypeLabels[type]}</option>)}</Select>
                          <Input label="Título / pergunta" value={field.label} disabled={!editable} onChange={event => updateField(field.id, { label: event.target.value })} />
                        </div>
                        <div className="grid gap-3 md:grid-cols-2">
                          <Input label="Texto auxiliar" value={field.description} disabled={!editable} onChange={event => updateField(field.id, { description: event.target.value })} />
                          <Input label="Placeholder" value={field.placeholder} disabled={!editable || displayFieldTypes.includes(field.type)} onChange={event => updateField(field.id, { placeholder: event.target.value })} />
                        </div>
                        {optionFieldTypes.includes(field.type) && <div className="space-y-1.5"><label className="block text-xs font-medium text-slate-300">Opções — uma por linha</label><textarea rows={Math.min(6, Math.max(2, field.options.length))} disabled={!editable} value={field.options.map(option => option.label).join('\n')} onChange={event => updateOptions(field, event.target.value)} className="w-full rounded-lg border border-slate-800 bg-slate-950/70 px-3 py-2 text-sm text-slate-100 focus:border-rose-500 focus:outline-none" /></div>}
                        {!displayFieldTypes.includes(field.type) && <label className="flex items-center gap-2 text-xs text-slate-300"><input type="checkbox" disabled={!editable} checked={field.required} onChange={event => updateField(field.id, { required: event.target.checked })} /> Resposta obrigatória</label>}
                      </div>
                    ))}
                  </div>
                )}
              </Card>

              <div className="space-y-6">
                <Card className="space-y-4 2xl:sticky 2xl:top-6">
                  <h2 className="flex items-center gap-2 border-b border-slate-800 pb-3 font-display font-semibold text-rose-400"><Eye className="h-4 w-4" /> Pré-visualização</h2>
                  <FormPreview schema={draft.schema} />
                </Card>
              </div>
            </div>
          </div>
        )}
      </div>

      <Dialog isOpen={createOpen} onClose={() => setCreateOpen(false)} title="Criar Formulário">
        <form onSubmit={createTemplate} className="space-y-4">
          <Input label="Nome" required minLength={2} maxLength={160} value={createData.name} onChange={event => setCreateData({ ...createData, name: event.target.value })} />
          <Input label="Categoria" required minLength={2} maxLength={100} value={createData.category} onChange={event => setCreateData({ ...createData, category: event.target.value })} />
          <Input label="Descrição" maxLength={1000} value={createData.description} onChange={event => setCreateData({ ...createData, description: event.target.value })} />
          <Button type="submit" className="w-full">Criar rascunho</Button>
        </form>
      </Dialog>

      <Dialog isOpen={publishOpen} onClose={() => setPublishOpen(false)} title="Publicar Nova Versão">
        <form onSubmit={publish} className="space-y-4">
          <p className="text-xs leading-relaxed text-slate-400">A publicação cria uma cópia imutável do schema atual. Alterações futuras gerarão outra versão.</p>
          <Input label="Resumo das alterações" required minLength={3} maxLength={500} value={changeSummary} onChange={event => setChangeSummary(event.target.value)} />
          <Button type="submit" className="w-full">Confirmar e publicar</Button>
        </form>
      </Dialog>

      <Dialog isOpen={duplicateOpen} onClose={() => setDuplicateOpen(false)} title="Duplicar Formulário">
        <form onSubmit={duplicate} className="space-y-4">
          <Input label="Nome da nova cópia" required minLength={2} maxLength={160} value={duplicateName} onChange={event => setDuplicateName(event.target.value)} />
          <Button type="submit" className="w-full">Criar cópia em rascunho</Button>
        </form>
      </Dialog>

      <Dialog isOpen={versionOpen} onClose={() => setVersionOpen(false)} title={selectedVersion ? `Versão ${selectedVersion.versionNumber} — somente leitura` : 'Versão publicada'} maxWidth="max-w-3xl">
        {selectedVersion && <div className="space-y-5"><div className="rounded-lg border border-emerald-500/20 bg-emerald-500/5 p-3 text-xs text-emerald-300"><strong>Publicada em:</strong> {new Date(selectedVersion.publishedAtUtc).toLocaleString('pt-BR')}<br /><strong>Hash do schema:</strong> <span className="font-mono text-[10px]">{selectedVersion.schemaHash}</span><br /><strong>Resumo:</strong> {selectedVersion.changeSummary}</div><FormPreview schema={selectedVersion.schema} /></div>}
      </Dialog>
      {confirmationDialog}
    </div>
  );
};
