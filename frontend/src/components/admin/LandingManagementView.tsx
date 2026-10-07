import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Clock3, ImagePlus, Images, Mail, MessageCircle, Plus, RefreshCw, Trash2, Upload, UsersRound } from 'lucide-react';
import { CmsContent, LandingCarouselItem, Lead } from '../../types';
import { api } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, CardHeader, Input, Select } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';

const leadStatuses = ['Novo', 'Em contato', 'Avaliação agendada', 'Convertido', 'Encerrado'];

const statusVariant = (status: string): 'success' | 'warning' | 'info' | 'danger' | 'default' => {
  if (status === 'Convertido') return 'success';
  if (status === 'Avaliação agendada') return 'info';
  if (status === 'Em contato') return 'warning';
  if (status === 'Encerrado') return 'default';
  return 'danger';
};

const sourceLabel: Record<Lead['source'], string> = {
  Instagram: 'Instagram',
  Indication: 'Indicação',
  GoogleAds: 'Google Ads',
  WhatsApp: 'WhatsApp',
  WalkIn: 'Presencial',
  Other: 'Outro',
  Website: 'Landing page',
};

const formatLeadDate = (value: string) => new Date(value).toLocaleString('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'short',
});

const whatsappUrl = (phone: string) => {
  const number = phone.replace(/\D/g, '');
  return number ? `https://wa.me/${number}` : undefined;
};

const MAX_CAROUSEL_ITEMS = 8;

const createCarouselItem = (): LandingCarouselItem => ({
  id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `gallery-${Date.now()}-${Math.random().toString(16).slice(2)}`,
  imageUrl: '',
  title: '',
  description: '',
});

const LandingImageField: React.FC<{
  title: string;
  helper: string;
  value: string;
  onChange: (value: string) => void;
  onFileSelect: (file: File | null) => void;
  isUploading: boolean;
  previewFit?: 'cover' | 'contain';
  fileHelper?: string;
}> = ({ title, helper, value, onChange, onFileSelect, isUploading, previewFit = 'cover', fileHelper = 'JPG, PNG ou WEBP, até 25 MB.' }) => {
  const [previewAvailable, setPreviewAvailable] = useState(true);

  useEffect(() => setPreviewAvailable(true), [value]);

  return <div className="grid gap-4 rounded-xl border border-slate-800 bg-slate-950/40 p-4 sm:grid-cols-[8.5rem_minmax(0,1fr)]">
    <div className="overflow-hidden rounded-lg border border-slate-700 bg-slate-900/60">
      {value && previewAvailable ? <img src={value} alt="Prévia da imagem" onLoad={() => setPreviewAvailable(true)} onError={() => setPreviewAvailable(false)} className={`h-32 w-full ${previewFit === 'contain' ? 'object-contain p-3' : 'object-cover'}`} /> : <div className="flex h-32 items-center justify-center text-slate-500"><Images className="h-7 w-7" /></div>}
    </div>
    <div className="space-y-3">
      <div><h3 className="font-display text-sm font-semibold text-white">{title}</h3><p className="mt-1 text-xs leading-relaxed text-slate-400">{helper}</p></div>
      <Input label="Link da imagem (opcional)" type="text" inputMode="url" maxLength={500} placeholder="https://..." value={value} onChange={event => { setPreviewAvailable(true); onChange(event.target.value); }} />
      {!previewAvailable && <p className="-mt-1 text-xs text-rose-400">Não foi possível carregar este link. Verifique se a imagem é pública e usa HTTPS.</p>}
      <label className="flex cursor-pointer items-center justify-between gap-4 rounded-lg border border-dashed border-slate-700 bg-slate-900/50 px-3 py-2.5 text-sm text-slate-200 transition-colors hover:border-rose-500/60">
        <span><strong>Enviar arquivo</strong><span className="mt-0.5 block text-[11px] text-slate-500">{fileHelper}</span></span>
        <span className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-xs font-medium"><Upload className="h-3.5 w-3.5" /> {isUploading ? 'Enviando...' : 'Escolher imagem'}</span>
        <input className="sr-only" type="file" disabled={isUploading} accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp" onChange={event => { onFileSelect(event.target.files?.[0] ?? null); event.currentTarget.value = ''; }} />
      </label>
    </div>
  </div>;
};

export const LandingManagementView: React.FC<{
  cms: CmsContent;
  onCmsChange: (content: CmsContent) => void;
}> = ({ cms, onCmsChange }) => {
  const { can } = useAuth();
  const { showToast } = useToast();
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [logoOnDarkFile, setLogoOnDarkFile] = useState<File | null>(null);
  const [logoOnLightFile, setLogoOnLightFile] = useState<File | null>(null);
  const [uploadingMediaKey, setUploadingMediaKey] = useState<string | null>(null);
  const [isPhotoPreviewAvailable, setIsPhotoPreviewAvailable] = useState(true);
  const [isSavingCms, setIsSavingCms] = useState(false);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [isLoadingLeads, setIsLoadingLeads] = useState(false);
  const [updatingLeadId, setUpdatingLeadId] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const canReadLeads = can('leads.read');
  const canManageLeads = can('leads.manage');

  const loadLeads = useCallback(async () => {
    if (!canReadLeads) return;
    try {
      setIsLoadingLeads(true);
      setLeads(await api.getLeads());
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível carregar as solicitações.', 'error');
    } finally {
      setIsLoadingLeads(false);
    }
  }, [canReadLeads, showToast]);

  useEffect(() => { void loadLeads(); }, [loadLeads]);

  const visibleLeads = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase('pt-BR');
    return leads.filter(lead => {
      const matchesStatus = statusFilter === 'all' || lead.status === statusFilter;
      const searchable = `${lead.name} ${lead.phone} ${lead.email} ${lead.interestedProcedure} ${lead.message || ''}`.toLocaleLowerCase('pt-BR');
      return matchesStatus && (!normalizedQuery || searchable.includes(normalizedQuery));
    });
  }, [leads, query, statusFilter]);

  const newLeadCount = leads.filter(lead => lead.status === 'Novo').length;
  const websiteLeadCount = leads.filter(lead => lead.source === 'Website').length;

  const selectPhoto = (file: File | null) => {
    if (!file) return;
    if (file.size > 25 * 1024 * 1024) {
      showToast('A foto da profissional deve ter no máximo 25 MB.', 'error');
      return;
    }
    setPhotoFile(file);
  };

  const selectLogo = (file: File | null, onSelect: (file: File) => void) => {
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      showToast('Cada versão da logo deve ter no máximo 5 MB.', 'error');
      return;
    }
    onSelect(file);
  };

  const uploadContentImage = async (file: File | null, mediaKey: string, onUploaded: (url: string) => void) => {
    if (!file) return;
    if (file.size > 25 * 1024 * 1024) {
      showToast('A imagem deve ter no máximo 25 MB.', 'error');
      return;
    }

    try {
      setUploadingMediaKey(mediaKey);
      const uploaded = await api.uploadCmsImage(file);
      onUploaded(uploaded.url);
      showToast('Imagem enviada. Publique as alterações para exibi-la na landing page.', 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível enviar a imagem.', 'error');
    } finally {
      setUploadingMediaKey(null);
    }
  };

  const saveCms = async (event: React.FormEvent) => {
    event.preventDefault();
    const photoUrl = cms.doctorPhotoUrl.trim();
    if (!photoFile && photoUrl && !photoUrl.startsWith('/api/public/cms/') && !photoUrl.startsWith('/brand/') && !photoUrl.startsWith('https://')) {
      showToast('Use um link de imagem público iniciado com https:// ou envie um arquivo.', 'error');
      return;
    }
    try {
      setIsSavingCms(true);
      let updated = await api.updateCms(cms);
      if (photoFile) updated = await api.uploadCmsPhoto(photoFile);
      if (logoOnDarkFile) updated = await api.uploadCmsLogo('dark', logoOnDarkFile);
      if (logoOnLightFile) updated = await api.uploadCmsLogo('light', logoOnLightFile);
      onCmsChange(updated);
      setPhotoFile(null);
      setLogoOnDarkFile(null);
      setLogoOnLightFile(null);
      showToast(
        photoFile || logoOnDarkFile || logoOnLightFile
          ? 'Landing page e identidade visual atualizadas.'
          : 'Conteúdo da landing page atualizado.',
        'success',
      );
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível publicar as alterações da landing page.', 'error');
    } finally {
      setIsSavingCms(false);
    }
  };

  const updateLeadStatus = async (lead: Lead, status: string) => {
    if (status === lead.status) return;
    try {
      setUpdatingLeadId(lead.id);
      const updated = await api.updateLeadStatus(lead.id, status);
      setLeads(current => current.map(item => item.id === updated.id ? updated : item));
      showToast(`Contato de ${updated.name} atualizado para “${updated.status}”.`, 'success');
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Não foi possível atualizar o status do contato.', 'error');
    } finally {
      setUpdatingLeadId(null);
    }
  };

  const carouselItems = cms.carouselItems ?? [];
  const updateCarouselItem = (id: string, changes: Partial<LandingCarouselItem>) => onCmsChange({
    ...cms,
    carouselItems: carouselItems.map(item => item.id === id ? { ...item, ...changes } : item),
  });

  const addCarouselItem = () => {
    if (carouselItems.length >= MAX_CAROUSEL_ITEMS) {
      showToast(`A galeria aceita no máximo ${MAX_CAROUSEL_ITEMS} imagens.`, 'error');
      return;
    }
    onCmsChange({ ...cms, carouselItems: [...carouselItems, createCarouselItem()] });
  };

  return (
    <div className="flex flex-col gap-6">
      <AdminPageHeader className="order-1" title="Gestão da landing page" description="Publique a apresentação da profissional e acompanhe as solicitações enviadas pelo site." />

      <Card className="order-3">
        <CardHeader title="Conteúdo público" subtitle="A foto oficial já é o padrão. Os blocos opcionais só aparecem na landing page quando forem preenchidos e publicados." />
        <form onSubmit={saveCms} className="space-y-4">
          <section className="space-y-4 rounded-xl border border-slate-800 bg-slate-950/40 p-4">
            <div>
              <h2 className="font-display text-lg font-semibold text-white">Logo da landing page</h2>
              <p className="mt-1 text-xs leading-relaxed text-slate-400">A marca atual já vem configurada em duas versões: clara para o modo escuro e escura para o modo claro. Elas aparecem tanto no cabeçalho quanto no rodapé.</p>
            </div>
            <div className="grid gap-4 xl:grid-cols-2">
              <div className="space-y-2">
                <LandingImageField
                  title="Versão para fundo escuro"
                  helper="Use a versão clara da marca. Ela é exibida quando a landing está no modo escuro."
                  value={cms.logoOnDarkUrl ?? ''}
                  onChange={value => onCmsChange({ ...cms, logoOnDarkUrl: value })}
                  isUploading={isSavingCms}
                  previewFit="contain"
                  fileHelper="JPG, PNG ou WEBP; até 5 MB. Preferência horizontal, mínimo 320 × 80 px."
                  onFileSelect={file => selectLogo(file, setLogoOnDarkFile)}
                />
                {logoOnDarkFile && <div className="flex items-center justify-between gap-3 rounded-md border border-emerald-500/20 bg-emerald-500/5 px-3 py-2 text-xs text-emerald-300"><span className="truncate">Arquivo selecionado: {logoOnDarkFile.name}</span><button type="button" onClick={() => setLogoOnDarkFile(null)} className="shrink-0 text-slate-400 hover:text-white">Remover</button></div>}
              </div>
              <div className="space-y-2">
                <LandingImageField
                  title="Versão para fundo claro"
                  helper="Use a versão escura da marca. Ela é exibida quando a landing está no modo claro."
                  value={cms.logoOnLightUrl ?? ''}
                  onChange={value => onCmsChange({ ...cms, logoOnLightUrl: value })}
                  isUploading={isSavingCms}
                  previewFit="contain"
                  fileHelper="JPG, PNG ou WEBP; até 5 MB. Preferência horizontal, mínimo 320 × 80 px."
                  onFileSelect={file => selectLogo(file, setLogoOnLightFile)}
                />
                {logoOnLightFile && <div className="flex items-center justify-between gap-3 rounded-md border border-emerald-500/20 bg-emerald-500/5 px-3 py-2 text-xs text-emerald-300"><span className="truncate">Arquivo selecionado: {logoOnLightFile.name}</span><button type="button" onClick={() => setLogoOnLightFile(null)} className="shrink-0 text-slate-400 hover:text-white">Remover</button></div>}
              </div>
            </div>
            <p className="text-[11px] leading-relaxed text-slate-500">Arquivos enviados são validados, convertidos para PNG transparente e ajustados sem distorção ao máximo de 1.600 × 600 px. Links externos devem ser públicos e usar HTTPS.</p>
          </section>

          <Input label="Título principal" required maxLength={240} value={cms.heroTitle} onChange={event => onCmsChange({ ...cms, heroTitle: event.target.value })} />
          <Input label="Subtítulo principal" required maxLength={500} value={cms.heroSubtitle} onChange={event => onCmsChange({ ...cms, heroSubtitle: event.target.value })} />
          <div className="grid gap-4 md:grid-cols-2">
            <Input label="Nome da profissional" required maxLength={160} value={cms.doctorName} onChange={event => onCmsChange({ ...cms, doctorName: event.target.value })} />
            <Input label="Título / especialidade" required maxLength={200} value={cms.doctorTitle} onChange={event => onCmsChange({ ...cms, doctorTitle: event.target.value })} />
          </div>
          <Input label="Biografia / apresentação" required maxLength={3000} value={cms.doctorBio} onChange={event => onCmsChange({ ...cms, doctorBio: event.target.value })} />
          <div className="grid gap-4 md:grid-cols-2">
            <Input label="WhatsApp com DDD" required maxLength={30} inputMode="tel" value={cms.whatsappNumber} onChange={event => onCmsChange({ ...cms, whatsappNumber: event.target.value.replace(/\D/g, '') })} />
            <Input label="Endereço da clínica" maxLength={300} value={cms.addressText} onChange={event => onCmsChange({ ...cms, addressText: event.target.value })} />
          </div>

          <section className="space-y-4 rounded-xl border border-slate-800 bg-slate-950/35 p-4">
            <div className="flex items-start gap-3"><span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-sky-500/25 bg-sky-500/10 text-sky-400"><Clock3 className="h-4 w-4" /></span><div><h2 className="font-display text-base font-semibold text-white">Horário de funcionamento</h2><p className="mt-1 text-xs leading-relaxed text-slate-400">Informação exibida publicamente no rodapé da landing page. Ela não altera o expediente da agenda interna.</p></div></div>
            <div className="grid gap-4 md:grid-cols-2"><Input label="Segunda a sexta" maxLength={120} placeholder="Ex.: Segunda a Sexta: 08h00 às 19h00" value={cms.publicHoursWeekdays ?? ''} onChange={event => onCmsChange({ ...cms, publicHoursWeekdays: event.target.value })} /><Input label="Sábados" maxLength={120} placeholder="Ex.: Sábados: 08h00 às 13h00" value={cms.publicHoursSaturday ?? ''} onChange={event => onCmsChange({ ...cms, publicHoursSaturday: event.target.value })} /></div>
            <Input label="Observação (opcional)" maxLength={240} placeholder="Ex.: Atendimento somente com horário agendado" value={cms.publicHoursNote ?? ''} onChange={event => onCmsChange({ ...cms, publicHoursNote: event.target.value })} />
            <p className="text-[11px] text-slate-500">Deixe todos os campos vazios para ocultar esta coluna do rodapé.</p>
          </section>

          <div className="grid gap-5 rounded-xl border border-slate-800 bg-slate-950/40 p-4 lg:grid-cols-[10rem_minmax(0,1fr)]">
            <div className="overflow-hidden rounded-lg border border-slate-700 bg-slate-900/60">
              {cms.doctorPhotoUrl && isPhotoPreviewAvailable ? <img src={cms.doctorPhotoUrl} alt={`Foto de ${cms.doctorName}`} onLoad={() => setIsPhotoPreviewAvailable(true)} onError={() => setIsPhotoPreviewAvailable(false)} className="h-40 w-full object-cover" /> : <div className="flex h-40 items-center justify-center text-slate-500"><ImagePlus className="h-7 w-7" /></div>}
            </div>
            <div className="space-y-4">
              <div>
                <h2 className="font-display text-base font-semibold text-white">Foto da profissional</h2>
                <p className="mt-1 text-xs leading-relaxed text-slate-400">Use um link público com HTTPS ou envie JPG, PNG ou WEBP. O arquivo é validado, normalizado e reduzido quando necessário.</p>
              </div>
              <Input label="Link da imagem (opcional)" type="text" inputMode="url" maxLength={500} placeholder="https://..." value={cms.doctorPhotoUrl} onChange={event => { setIsPhotoPreviewAvailable(true); onCmsChange({ ...cms, doctorPhotoUrl: event.target.value }); }} />
              {!isPhotoPreviewAvailable && <p className="-mt-2 text-xs text-rose-400">Não foi possível carregar este link. Verifique se ele é uma imagem pública e usa HTTPS.</p>}
              <label className="flex cursor-pointer items-center justify-between gap-4 rounded-lg border border-dashed border-slate-700 bg-slate-900/50 px-3 py-3 text-sm text-slate-200 transition-colors hover:border-rose-500/60">
                <span><strong>Enviar arquivo</strong><span className="mt-0.5 block text-[11px] text-slate-500">Máximo de 25 MB. Ao selecionar, o arquivo substitui o link ao publicar.</span></span>
                <span className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-xs font-medium"><Upload className="h-3.5 w-3.5" /> Escolher foto</span>
                <input className="sr-only" type="file" accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp" onChange={event => selectPhoto(event.target.files?.[0] ?? null)} />
              </label>
              {photoFile && <div className="flex items-center justify-between gap-3 rounded-md border border-emerald-500/20 bg-emerald-500/5 px-3 py-2 text-xs text-emerald-300"><span className="truncate">Arquivo selecionado: {photoFile.name}</span><button type="button" onClick={() => setPhotoFile(null)} className="shrink-0 text-slate-400 hover:text-white">Remover</button></div>}
            </div>
          </div>

          <div className="space-y-4 border-t border-slate-800 pt-5">
            <div><h2 className="font-display text-lg font-semibold text-white">Sobre a profissional</h2><p className="mt-1 text-xs text-slate-400">Um bloco elegante com foto e texto. Ele não será exibido enquanto título e texto estiverem vazios.</p></div>
            <Input label="Título da seção" maxLength={160} placeholder="Ex.: Cuidado que começa pela escuta" value={cms.aboutTitle ?? ''} onChange={event => onCmsChange({ ...cms, aboutTitle: event.target.value })} />
            <div className="space-y-1.5"><label className="block text-xs font-medium text-slate-300">Texto da seção</label><textarea rows={5} maxLength={3000} placeholder="Conte quem ela é, sua abordagem e o que torna o atendimento especial..." value={cms.aboutText ?? ''} onChange={event => onCmsChange({ ...cms, aboutText: event.target.value })} className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm text-slate-100 transition-all focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50" /></div>
            <LandingImageField title="Foto do bloco Sobre" helper="Se ficar vazio, a landing usa a foto principal da profissional." value={cms.aboutImageUrl ?? ''} onChange={value => onCmsChange({ ...cms, aboutImageUrl: value })} isUploading={uploadingMediaKey === 'about'} onFileSelect={file => void uploadContentImage(file, 'about', url => onCmsChange({ ...cms, aboutImageUrl: url }))} />
          </div>

          <div className="space-y-4 border-t border-slate-800 pt-5">
            <div><h2 className="font-display text-lg font-semibold text-white">A clínica</h2><p className="mt-1 text-xs text-slate-400">O layout alterna a posição da imagem e do texto para deixar a apresentação mais editorial.</p></div>
            <Input label="Título da seção" maxLength={160} placeholder="Ex.: Um espaço pensado para você" value={cms.clinicTitle ?? ''} onChange={event => onCmsChange({ ...cms, clinicTitle: event.target.value })} />
            <div className="space-y-1.5"><label className="block text-xs font-medium text-slate-300">Texto da seção</label><textarea rows={5} maxLength={3000} placeholder="Descreva o ambiente, localização e experiência da clínica..." value={cms.clinicText ?? ''} onChange={event => onCmsChange({ ...cms, clinicText: event.target.value })} className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm text-slate-100 transition-all focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50" /></div>
            <LandingImageField title="Foto da clínica" helper="Envie uma imagem do ambiente ou use um link público. A seção também pode ser publicada apenas com texto." value={cms.clinicImageUrl ?? ''} onChange={value => onCmsChange({ ...cms, clinicImageUrl: value })} isUploading={uploadingMediaKey === 'clinic'} onFileSelect={file => void uploadContentImage(file, 'clinic', url => onCmsChange({ ...cms, clinicImageUrl: url }))} />
          </div>

          <div className="space-y-4 border-t border-slate-800 pt-5">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between"><div><h2 className="font-display text-lg font-semibold text-white">Galeria de imagens</h2><p className="mt-1 text-xs text-slate-400">Mostre detalhes, ambiente, resultados autorizados ou imagens de campanha. Sem imagens, nenhuma galeria aparece no site.</p></div><Button type="button" variant="outline" size="sm" onClick={addCarouselItem} disabled={carouselItems.length >= MAX_CAROUSEL_ITEMS}><Plus className="h-3.5 w-3.5" /> Adicionar imagem</Button></div>
            {carouselItems.length === 0 ? <div className="rounded-xl border border-dashed border-slate-700 bg-slate-950/30 py-7 text-center"><Images className="mx-auto h-7 w-7 text-slate-600" /><p className="mt-2 text-sm font-medium text-slate-300">A galeria está vazia.</p><p className="mt-1 text-xs text-slate-500">Ela permanecerá oculta na landing page até receber uma imagem.</p></div> : <div className="space-y-4">
              {carouselItems.map((item, index) => <div key={item.id} className="space-y-4 rounded-xl border border-slate-800 bg-slate-950/25 p-4"><div className="flex items-center justify-between gap-3"><h3 className="font-display text-sm font-semibold text-white">Imagem {index + 1}</h3><Button type="button" variant="ghost" size="sm" onClick={() => onCmsChange({ ...cms, carouselItems: carouselItems.filter(current => current.id !== item.id) })}><Trash2 className="h-3.5 w-3.5 text-rose-400" /> Remover</Button></div><LandingImageField title="Imagem da galeria" helper="Use uma foto horizontal para melhor resultado no carrossel." value={item.imageUrl} onChange={value => updateCarouselItem(item.id, { imageUrl: value })} isUploading={uploadingMediaKey === item.id} onFileSelect={file => void uploadContentImage(file, item.id, url => updateCarouselItem(item.id, { imageUrl: url }))} /><div className="grid gap-4 md:grid-cols-2"><Input label="Título (opcional)" maxLength={160} value={item.title} onChange={event => updateCarouselItem(item.id, { title: event.target.value })} /><div className="space-y-1.5"><label className="block text-xs font-medium text-slate-300">Descrição (opcional)</label><textarea rows={2} maxLength={600} value={item.description} onChange={event => updateCarouselItem(item.id, { description: event.target.value })} className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 text-sm text-slate-100 transition-all focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50" /></div></div></div>)}
            </div>}
          </div>

          <div className="flex justify-end pt-1"><Button type="submit" variant="primary" disabled={isSavingCms}>{isSavingCms ? 'Publicando...' : 'Publicar alterações'}</Button></div>
        </form>
      </Card>

      {canReadLeads && <Card className="order-2 space-y-5">
        <CardHeader
          title="Solicitações de avaliação"
          subtitle="Contatos recebidos pelo formulário “Agende sua avaliação” da landing page."
          action={<Button type="button" variant="outline" size="sm" onClick={() => void loadLeads()} disabled={isLoadingLeads}><RefreshCw className={`h-3.5 w-3.5 ${isLoadingLeads ? 'animate-spin' : ''}`} /> Atualizar</Button>}
        />

        <div className="grid gap-3 sm:grid-cols-3">
          <div className="rounded-xl border border-rose-500/20 bg-rose-500/5 p-3"><span className="text-[10px] font-semibold uppercase tracking-wide text-rose-300">Aguardando retorno</span><p className="mt-1 font-display text-2xl font-bold text-rose-400">{newLeadCount}</p></div>
          <div className="rounded-xl border border-sky-500/20 bg-sky-500/5 p-3"><span className="text-[10px] font-semibold uppercase tracking-wide text-sky-300">Do site</span><p className="mt-1 font-display text-2xl font-bold text-sky-400">{websiteLeadCount}</p></div>
          <div className="rounded-xl border border-emerald-500/20 bg-emerald-500/5 p-3"><span className="text-[10px] font-semibold uppercase tracking-wide text-emerald-300">Convertidos</span><p className="mt-1 font-display text-2xl font-bold text-emerald-400">{leads.filter(lead => lead.status === 'Convertido').length}</p></div>
        </div>

        <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_13rem]">
          <Input aria-label="Buscar solicitações" placeholder="Buscar por nome, telefone ou procedimento..." value={query} onChange={event => setQuery(event.target.value)} />
          <Select aria-label="Filtrar por status" value={statusFilter} onChange={event => setStatusFilter(event.target.value)}>
            <option value="all">Todos os status</option>
            {leadStatuses.map(status => <option key={status} value={status}>{status}</option>)}
          </Select>
        </div>

        {isLoadingLeads ? <div className="flex items-center justify-center gap-2 py-10 text-sm text-slate-400"><RefreshCw className="h-4 w-4 animate-spin" /> Carregando solicitações...</div> : visibleLeads.length === 0 ? <div className="py-10 text-center"><UsersRound className="mx-auto h-8 w-8 text-slate-600" /><p className="mt-3 text-sm font-medium text-slate-300">Nenhuma solicitação encontrada.</p><p className="mt-1 text-xs text-slate-500">Os novos contatos enviados pela landing page aparecerão aqui.</p></div> : <div className="max-h-[34rem] space-y-3 overflow-y-auto overscroll-contain pr-1">
          {visibleLeads.map(lead => {
            const whatsapp = whatsappUrl(lead.phone);
            return <article key={lead.id} className="rounded-xl border border-slate-800 bg-slate-950/45 p-4">
              <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2"><h3 className="font-display font-semibold text-white">{lead.name}</h3><Badge variant={statusVariant(lead.status)}>{lead.status}</Badge><Badge variant="default">{sourceLabel[lead.source]}</Badge></div>
                  <p className="mt-1 flex items-center gap-1.5 text-xs text-slate-500"><Clock3 className="h-3.5 w-3.5" /> Recebido em {formatLeadDate(lead.createdAt)}</p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {whatsapp && <a href={whatsapp} target="_blank" rel="noopener noreferrer"><Button type="button" variant="outline" size="sm"><MessageCircle className="h-3.5 w-3.5 text-emerald-400" /> WhatsApp</Button></a>}
                  {lead.email && <a href={`mailto:${lead.email}`}><Button type="button" variant="outline" size="sm"><Mail className="h-3.5 w-3.5" /> E-mail</Button></a>}
                </div>
              </div>
              <div className="mt-3 grid gap-3 border-t border-slate-800 pt-3 md:grid-cols-[minmax(0,1fr)_12rem]">
                <div className="min-w-0 text-xs text-slate-400"><p><span className="text-slate-500">Telefone:</span> {lead.phone}</p>{lead.email && <p className="mt-1 break-all"><span className="text-slate-500">E-mail:</span> {lead.email}</p>}<p className="mt-1"><span className="text-slate-500">Interesse:</span> {lead.interestedProcedure || 'Avaliação geral'}</p>{lead.message && <p className="mt-2 rounded-md bg-slate-900/70 p-2 leading-relaxed text-slate-300">{lead.message}</p>}</div>
                <Select label="Status do contato" value={lead.status} disabled={!canManageLeads || updatingLeadId === lead.id} onChange={event => void updateLeadStatus(lead, event.target.value)}>
                  {leadStatuses.map(status => <option key={status} value={status}>{status}</option>)}
                </Select>
              </div>
            </article>;
          })}
        </div>}
      </Card>}

      {!canReadLeads && <Card className="order-2 flex items-center gap-3 border-amber-500/25 bg-amber-500/5 text-sm text-amber-200"><CheckCircle2 className="h-5 w-5 shrink-0" /> Seu acesso pode editar a landing page, mas não permite visualizar os contatos recebidos.</Card>}
    </div>
  );
};
