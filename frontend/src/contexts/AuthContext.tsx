import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { api, UNAUTHORIZED_EVENT } from '../services/api';
import { LoginResponse, MfaRecoveryCodes, MfaSetup, User } from '../types';

interface AuthContextType {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: string | null;
  currentUser: User | null;
  roles: string[];
  permissions: string[];
  login: (username: string, password: string) => Promise<LoginResponse['status']>;
  loginMfa: (code: string) => Promise<void>;
  loginRecovery: (code: string) => Promise<void>;
  logout: () => Promise<void>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>;
  setupMfa: () => Promise<MfaSetup>;
  enableMfa: (code: string) => Promise<MfaRecoveryCodes>;
  refresh: () => Promise<void>;
  can: (permission: string) => boolean;
}

const AuthContext = createContext<AuthContextType>({} as AuthContextType);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const clear = () => setCurrentUser(null);

  const refresh = async () => {
    const authenticatedUser = await api.me();
    setCurrentUser(authenticatedUser);
  };

  useEffect(() => {
    window.addEventListener(UNAUTHORIZED_EVENT, clear);
    refresh().catch(clear).finally(() => setIsLoading(false));
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, clear);
  }, []);

  const login = async (username: string, password: string) => {
    const response = await api.login(username, password);
    if (response.status === 'Authenticated' && response.user) setCurrentUser(response.user);
    return response.status;
  };

  const loginMfa = async (code: string) => {
    const response = await api.loginMfa(code);
    if (!response.user) throw new Error('A autenticação multifator não foi concluída.');
    setCurrentUser(response.user);
  };

  const loginRecovery = async (code: string) => {
    const response = await api.loginRecovery(code);
    if (!response.user) throw new Error('O código de recuperação não foi aceito.');
    setCurrentUser(response.user);
  };

  const logout = async () => {
    try { await api.logout(); } finally { clear(); }
  };

  const changePassword = async (currentPassword: string, newPassword: string) => {
    await api.changePassword(currentPassword, newPassword);
    await refresh();
  };

  const enableMfa = async (code: string) => {
    return await api.enableMfa(code);
  };

  const value = useMemo<AuthContextType>(() => ({
    isAuthenticated: Boolean(currentUser),
    isLoading,
    user: currentUser?.username ?? null,
    currentUser,
    roles: currentUser?.roles ?? [],
    permissions: currentUser?.permissions ?? [],
    login,
    loginMfa,
    loginRecovery,
    logout,
    changePassword,
    setupMfa: api.setupMfa,
    enableMfa,
    refresh,
    can: permission => currentUser?.permissions.includes(permission) ?? false,
  }), [currentUser, isLoading]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => useContext(AuthContext);
