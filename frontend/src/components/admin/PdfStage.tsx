import React, { useEffect, useRef, useState } from 'react';
import { ChevronLeft, ChevronRight, FileWarning, LoaderCircle } from 'lucide-react';
import { GlobalWorkerOptions, getDocument } from 'pdfjs-dist';
import type { PDFDocumentProxy, RenderTask } from 'pdfjs-dist';
import pdfWorkerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
import { Button } from '../ui/Components';

// O worker precisa ser configurado no mesmo carregamento do motor PDF. Separá-los
// em imports dinâmicos pode quebrar a resolução do worker em alguns builds do Vite.
// A versão na URL força a troca da resposta que navegadores podem ter guardado
// com MIME incorreto antes da configuração do Nginx ser corrigida.
const pdfWorkerVersionedUrl = `${pdfWorkerUrl}${pdfWorkerUrl.includes('?') ? '&' : '?'}v=20260810-3`;
GlobalWorkerOptions.workerSrc = pdfWorkerVersionedUrl;

export type PdfStageMetrics = { width: number; height: number };

interface PdfStageProps {
  pdfBytes: ArrayBuffer | null;
  pageNumber: number;
  onPageChange: (pageNumber: number) => void;
  onPageClick?: (pageNumber: number, x: number, y: number) => void;
  children?: (metrics: PdfStageMetrics) => React.ReactNode;
  label?: string;
}

export const PdfStage: React.FC<PdfStageProps> = ({ pdfBytes, pageNumber, onPageChange, onPageClick, children, label = 'Documento PDF' }) => {
  const hostRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [document, setDocument] = useState<PDFDocumentProxy | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  // Só renderizamos após conhecer a largura útil. Isso evita o primeiro paint
  // em 720px (valor de desktop) que fazia o PDF expandir o layout no iPhone.
  const [hostWidth, setHostWidth] = useState(0);
  const [metrics, setMetrics] = useState<PdfStageMetrics | null>(null);

  useEffect(() => {
    const host = hostRef.current;
    if (!host) return undefined;
    const updateWidth = () => {
      const computedStyle = window.getComputedStyle(host);
      const horizontalPadding = Number.parseFloat(computedStyle.paddingLeft) + Number.parseFloat(computedStyle.paddingRight);
      setHostWidth(Math.max(1, Math.floor(host.clientWidth - horizontalPadding)));
    };
    updateWidth();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(updateWidth);
    observer?.observe(host);
    window.addEventListener('resize', updateWidth);
    return () => {
      observer?.disconnect();
      window.removeEventListener('resize', updateWidth);
    };
  }, []);

  useEffect(() => {
    if (!pdfBytes) {
      setDocument(null);
      setMetrics(null);
      return undefined;
    }
    let active = true;
    setIsLoading(true);
    setError('');
    const loadingTask = getDocument({
      data: new Uint8Array(pdfBytes.slice(0)),
      // Um termo malformado deve falhar em vez de tentar uma recuperação
      // parcial que possa produzir uma visualização inconsistente.
      stopAtErrors: true,
    });
    void loadingTask.promise.then(next => {
      if (!active) {
        void next.cleanup();
        return;
      }
      setDocument(next);
      onPageChange(Math.min(Math.max(1, pageNumber), next.numPages));
    }).catch(error => {
      console.error('Falha ao abrir PDF no editor de termos.', error);
      if (active) setError('Não foi possível abrir este PDF. Remova senhas, corrija o arquivo e tente novamente.');
    }).finally(() => {
      if (active) setIsLoading(false);
    });
    return () => {
      active = false;
      loadingTask.destroy();
    };
  }, [pdfBytes]);

  useEffect(() => {
    if (!document || !canvasRef.current || !hostWidth) return undefined;
    let cancelled = false;
    let renderTask: RenderTask | null = null;
    void document.getPage(pageNumber).then(page => {
      if (cancelled || !canvasRef.current) return;
      const natural = page.getViewport({ scale: 1 });
      // A página nunca deve ficar mais larga que a área disponível nem ser
      // ampliada além de seu tamanho-base. Assim o palco não cria rolagem
      // horizontal ou cards gigantes em telas estreitas.
      const scale = Math.min(1, Math.max(.25, hostWidth / natural.width));
      const viewport = page.getViewport({ scale });
      const canvas = canvasRef.current;
      const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = Math.ceil(viewport.width * pixelRatio);
      canvas.height = Math.ceil(viewport.height * pixelRatio);
      canvas.style.width = `${Math.ceil(viewport.width)}px`;
      canvas.style.height = `${Math.ceil(viewport.height)}px`;
      const context = canvas.getContext('2d');
      if (!context) return;
      context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
      renderTask = page.render({ canvas, canvasContext: context, viewport });
      return renderTask.promise.then(() => {
        if (!cancelled) setMetrics({ width: Math.ceil(viewport.width), height: Math.ceil(viewport.height) });
      });
    }).catch(() => {
      if (!cancelled) setError('Não foi possível renderizar esta página do PDF.');
    });
    return () => {
      cancelled = true;
      renderTask?.cancel();
    };
  }, [document, pageNumber, hostWidth]);

  const handleCanvasClick = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!onPageClick) return;
    const rect = event.currentTarget.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    onPageClick(pageNumber, (event.clientX - rect.left) / rect.width, (event.clientY - rect.top) / rect.height);
  };

  return <div className="min-w-0 max-w-full space-y-3">
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-slate-800 bg-slate-950/50 px-3 py-2">
      <span className="text-xs text-slate-400">{label}{document ? ` • página ${pageNumber} de ${document.numPages}` : ''}</span>
      {document && <div className="flex items-center gap-2"><Button type="button" variant="outline" size="sm" disabled={pageNumber <= 1} onClick={() => onPageChange(pageNumber - 1)} aria-label="Página anterior"><ChevronLeft className="h-4 w-4" /></Button><Button type="button" variant="outline" size="sm" disabled={pageNumber >= document.numPages} onClick={() => onPageChange(pageNumber + 1)} aria-label="Próxima página"><ChevronRight className="h-4 w-4" /></Button></div>}
    </div>
    <div ref={hostRef} className="min-h-56 min-w-0 max-w-full overflow-hidden rounded-xl border border-slate-800 bg-slate-950/60 p-2 sm:p-3">
      {isLoading && <div className="flex min-h-56 items-center justify-center gap-2 text-sm text-slate-400"><LoaderCircle className="h-5 w-5 animate-spin" /> Abrindo PDF...</div>}
      {error && <div className="flex min-h-56 flex-col items-center justify-center text-center"><FileWarning className="h-8 w-8 text-rose-400" /><p className="mt-3 max-w-md text-sm text-slate-400">{error}</p></div>}
      {!isLoading && !error && !document && <div className="flex min-h-56 items-center justify-center text-sm text-slate-500">Envie ou selecione um PDF para visualizar.</div>}
      {!isLoading && !error && document && <div className="relative mx-auto" style={metrics ? { width: metrics.width, height: metrics.height } : undefined}>
        <canvas ref={canvasRef} onClick={handleCanvasClick} className={`block bg-white shadow-lg ${onPageClick ? 'cursor-crosshair' : ''}`} aria-label={`${label}, página ${pageNumber}`} />
        {metrics && children?.(metrics)}
      </div>}
    </div>
  </div>;
};
