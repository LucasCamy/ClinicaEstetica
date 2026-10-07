import React, { useEffect, useState } from 'react';
import { KeyRound, Plus, Shield, UserCheck, UserX } from 'lucide-react';
import { AccessCatalog, AdminUser, UserPermissionOverride } from '../../types';
import { api } from '../../services/api';
import { useToast } from '../../contexts/ToastContext';
import { Badge, Button, Card, Dialog, Input } from '../ui/Components';
import { AdminPageHeader } from './AdminPageHeader';

const permissionLabels: Record<string, string> = {
  'dashboard.read': 'Consultar dashboard',
  'clients.read': 'Consultar clientes',
  'clients.manage': 'Cadastrar e atualizar clientes',
  'clinical.read': 'Consultar prontuário e resumo do paciente',
  'clinical.manage': 'Alterar dados clínicos',
  'forms.read': 'Consultar modelos de formulários',
  'forms.manage': 'Criar e publicar formulários',
  'appointments.read': 'Consultar agenda',
  'appointments.manage': 'Gerenciar agenda',
  'procedures.read': 'Consultar procedimentos',
  'procedures.manage': 'Gerenciar procedimentos',
  'leads.read': 'Consultar contatos',
  'leads.manage': 'Gerenciar contatos',
  'cms.manage': 'Gerenciar landing page',
  'reports.read': 'Consultar financeiro e relatórios',
  'users.manage': 'Gerenciar usuários e acessos',
  'audit.read': 'Consultar auditoria',
};

type OverrideChoice = 'default' | 'grant' | 'deny';

export const UserAccessView: React.FC = () => {
  const { showToast } = useToast();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [catalog, setCatalog] = useState<AccessCatalog>({ roles: [], permissions: [] });
  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [roles, setRoles] = useState<string[]>([]);
  const [overrides, setOverrides] = useState<Record<string, OverrideChoice>>({});
  const [createForm, setCreateForm] = useState({ username: '', email: '', temporaryPassword: '', roles: ['Receptionist'] });

  const load = async () => {
    try {
      const [userPage, accessCatalog] = await Promise.all([api.getUsers(), api.getAccessCatalog()]);
      setUsers(userPage.items);
      setCatalog(accessCatalog);
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao carregar usuários.', 'error');
    }
  };

  useEffect(() => { void load(); }, []);

  const openAccess = (user: AdminUser) => {
    setEditing(user);
    setRoles([...user.roles]);
    const initial: Record<string, OverrideChoice> = {};
    catalog.permissions.forEach(permission => { initial[permission] = 'default'; });
    user.permissionOverrides.forEach(item => { initial[item.permission] = item.isGranted ? 'grant' : 'deny'; });
    setOverrides(initial);
  };

  const toggleRole = (role: string, checked: boolean, target: 'edit' | 'create') => {
    if (target === 'edit') setRoles(value => checked ? [...new Set([...value, role])] : value.filter(item => item !== role));
    else setCreateForm(value => ({ ...value, roles: checked ? [...new Set([...value.roles, role])] : value.roles.filter(item => item !== role) }));
  };

  const saveAccess = async () => {
    if (!editing) return;
    const permissionOverrides: UserPermissionOverride[] = Object.entries(overrides)
      .filter(([, choice]) => choice !== 'default')
      .map(([permission, choice]) => ({ permission, isGranted: choice === 'grant' }));
    try {
      await api.updateUserAccess(editing.id, roles, permissionOverrides);
      showToast('Papéis e permissões atualizados.', 'success');
      setEditing(null);
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao atualizar acessos.', 'error');
    }
  };

  const createUser = async (event: React.FormEvent) => {
    event.preventDefault();
    try {
      await api.createUser(createForm);
      setCreateOpen(false);
      setCreateForm({ username: '', email: '', temporaryPassword: '', roles: ['Receptionist'] });
      showToast('Usuário criado. A troca da senha será obrigatória no primeiro acesso.', 'success');
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao criar usuário.', 'error');
    }
  };

  const toggleStatus = async (user: AdminUser) => {
    try {
      await api.updateUserStatus(user.id, !user.isActive);
      showToast(user.isActive ? 'Usuário desativado.' : 'Usuário reativado.', 'success');
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao alterar o status.', 'error');
    }
  };

  const resetPassword = async (user: AdminUser) => {
    try {
      const result = await api.resetUserPassword(user.id);
      setTemporaryPassword(result.temporaryPassword);
      showToast('Senha temporária gerada. Ela será exibida apenas agora.', 'success');
      await load();
    } catch (error) {
      showToast(error instanceof Error ? error.message : 'Falha ao redefinir a senha.', 'error');
    }
  };

  return (
    <div className="space-y-6">
      <AdminPageHeader title="Usuários e acessos" description="Papéis definem o padrão; exceções por usuário ficam registradas na auditoria." actions={<Button onClick={() => setCreateOpen(true)}><Plus className="w-4 h-4" /> Novo usuário</Button>} />

      {temporaryPassword && (
        <Card className="border-amber-500/40 bg-amber-500/5">
          <div className="flex items-start gap-3">
            <KeyRound className="w-5 h-5 text-amber-400 mt-0.5" />
            <div className="space-y-2 min-w-0">
              <strong className="text-sm text-amber-300">Senha temporária — copie agora</strong>
              <code className="block break-all select-all text-white bg-slate-950 rounded p-3">{temporaryPassword}</code>
              <button className="text-xs text-slate-400 hover:text-white" onClick={() => setTemporaryPassword('')}>Ocultar senha</button>
            </div>
          </div>
        </Card>
      )}

      <div className="grid gap-4">
        {users.map(user => (
          <Card key={user.id} className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="space-y-2">
              <div className="flex items-center gap-2 flex-wrap">
                <strong className="text-white">{user.username}</strong>
                <Badge variant={user.isActive ? 'success' : 'danger'}>{user.isActive ? 'Ativo' : 'Inativo'}</Badge>
                {user.mustChangePassword && <Badge variant="warning">Troca de senha pendente</Badge>}
                {user.roles.some(role => role === 'Admin' || role === 'Professional') && (
                  <Badge variant={user.mfaEnabled ? 'success' : 'warning'}>{user.mfaEnabled ? 'MFA ativo' : 'MFA pendente'}</Badge>
                )}
              </div>
              <div className="text-xs text-slate-400">{user.email}</div>
              <div className="flex gap-1.5 flex-wrap">{user.roles.map(role => <Badge key={role}>{role}</Badge>)}</div>
            </div>
            <div className="flex gap-2 flex-wrap">
              <Button variant="outline" size="sm" onClick={() => openAccess(user)}><Shield className="w-4 h-4" /> Acessos</Button>
              <Button variant="outline" size="sm" onClick={() => void resetPassword(user)}><KeyRound className="w-4 h-4" /> Nova senha</Button>
              <Button variant="ghost" size="sm" onClick={() => void toggleStatus(user)}>
                {user.isActive ? <UserX className="w-4 h-4 text-rose-400" /> : <UserCheck className="w-4 h-4 text-emerald-400" />}
                {user.isActive ? 'Desativar' : 'Reativar'}
              </Button>
            </div>
          </Card>
        ))}
      </div>

      <Dialog isOpen={createOpen} onClose={() => setCreateOpen(false)} title="Criar usuário interno">
        <form className="space-y-4" onSubmit={createUser}>
          <Input label="Usuário" required minLength={3} value={createForm.username} onChange={e => setCreateForm({ ...createForm, username: e.target.value })} />
          <Input label="E-mail" type="email" required value={createForm.email} onChange={e => setCreateForm({ ...createForm, email: e.target.value })} />
          <Input label="Senha temporária" type="password" minLength={12} required value={createForm.temporaryPassword} onChange={e => setCreateForm({ ...createForm, temporaryPassword: e.target.value })} />
          <RoleChoices roles={catalog.roles} selected={createForm.roles} onToggle={(role, checked) => toggleRole(role, checked, 'create')} />
          <Button type="submit" className="w-full">Criar conta</Button>
        </form>
      </Dialog>

      <Dialog isOpen={Boolean(editing)} onClose={() => setEditing(null)} title={`Acessos de ${editing?.username ?? ''}`} maxWidth="max-w-3xl">
        <div className="space-y-6">
          <RoleChoices roles={catalog.roles} selected={roles} onToggle={(role, checked) => toggleRole(role, checked, 'edit')} />
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-white">Exceções individuais</h3>
            <p className="text-xs text-slate-400">“Padrão do papel” usa a matriz oficial. Permitir ou negar substitui esse padrão apenas para esta conta.</p>
            <div className="grid md:grid-cols-2 gap-3 max-h-80 overflow-y-auto pr-1">
              {catalog.permissions.map(permission => (
                <label key={permission} className="space-y-1 rounded-lg border border-slate-800 bg-slate-900/60 p-3">
                  <span className="block text-xs text-slate-200">{permissionLabels[permission] || permission}</span>
                  <select
                    className="w-full rounded bg-slate-950 border border-slate-700 p-2 text-xs text-slate-200"
                    value={overrides[permission] || 'default'}
                    onChange={e => setOverrides(value => ({ ...value, [permission]: e.target.value as OverrideChoice }))}
                  >
                    <option value="default">Padrão do papel</option>
                    <option value="grant">Permitir explicitamente</option>
                    <option value="deny">Negar explicitamente</option>
                  </select>
                </label>
              ))}
            </div>
          </div>
          <Button className="w-full" onClick={() => void saveAccess()} disabled={roles.length === 0}>Salvar papéis e permissões</Button>
        </div>
      </Dialog>
    </div>
  );
};

const RoleChoices: React.FC<{
  roles: string[];
  selected: string[];
  onToggle: (role: string, checked: boolean) => void;
}> = ({ roles, selected, onToggle }) => (
  <fieldset className="space-y-2">
    <legend className="text-xs font-semibold text-slate-300">Papéis</legend>
    <div className="flex flex-wrap gap-3">
      {roles.map(role => (
        <label key={role} className="flex items-center gap-2 text-xs text-slate-300">
          <input type="checkbox" checked={selected.includes(role)} onChange={event => onToggle(role, event.target.checked)} />
          {role}
        </label>
      ))}
    </div>
  </fieldset>
);
