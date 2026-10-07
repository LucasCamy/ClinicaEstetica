import React, { useEffect, useRef, useState } from 'react';
import { Check, Search } from 'lucide-react';
import { Client } from '../../types';

export const ClientSelectCombobox: React.FC<{
  clients: Client[];
  selectedClientId: string;
  onSelectClient: (client: Client) => void;
}> = ({ clients, selectedClientId, onSelectClient }) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const selectedClient = clients.find(client => client.id === selectedClientId);

  useEffect(() => {
    if (selectedClient) setSearchTerm(selectedClient.name);
  }, [selectedClient]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) setIsOpen(false);
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const filteredClients = clients.filter(client =>
    client.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    client.cpf.includes(searchTerm) ||
    client.phone.includes(searchTerm));

  return <div className="relative w-full space-y-1.5" ref={wrapperRef}>
    <label className="block text-xs font-medium text-slate-300">Buscar / Selecionar Cliente</label>
    <div className="relative">
      <input
        type="text"
        placeholder="Digite o nome, CPF ou WhatsApp do cliente..."
        className="w-full rounded-lg border border-slate-800 bg-slate-900/90 px-3.5 py-2 pl-9 text-sm text-slate-100 transition-all focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-500/50"
        value={searchTerm}
        onChange={event => { setSearchTerm(event.target.value); setIsOpen(true); }}
        onFocus={() => setIsOpen(true)}
      />
      <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" />
    </div>
    {isOpen && <div className="absolute z-50 mt-1 max-h-56 w-full divide-y divide-slate-800 overflow-y-auto rounded-xl border border-slate-700/80 bg-slate-900 shadow-2xl">
      {filteredClients.length === 0 ? <div className="p-3 text-center text-xs text-slate-500">Nenhum cliente encontrado.</div> : filteredClients.map(client => <button key={client.id} type="button" onClick={() => { onSelectClient(client); setSearchTerm(client.name); setIsOpen(false); }} className="flex w-full items-center justify-between p-2.5 text-left transition-colors hover:bg-slate-800/80">
        <div><div className="text-xs font-semibold text-white">{client.name}</div><div className="font-mono text-[11px] text-slate-400">CPF: {client.cpf || 'Sem CPF'} • {client.phone}</div></div>
        {client.id === selectedClientId && <Check className="h-4 w-4 text-rose-400" />}
      </button>)}
    </div>}
  </div>;
};
