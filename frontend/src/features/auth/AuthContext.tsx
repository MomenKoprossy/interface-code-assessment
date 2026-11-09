import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { login as loginRequest } from '../../api/auth';
import type { CurrentUser, Role } from '../../api/types';
import { clearSession, loadSession, saveSession, SESSION_EXPIRED_EVENT, type Session } from './session';

interface AuthContextValue {
  user: CurrentUser | null;
  /** True when the session ended because the server rejected the token (not a manual sign-out). */
  sessionExpired: boolean;
  login: (email: string, password: string) => Promise<CurrentUser>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function homePathFor(role: Role): string {
  return role === 'HR' ? '/employees' : '/me/payslip';
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => loadSession());
  const [sessionExpired, setSessionExpired] = useState(false);

  useEffect(() => {
    const onExpired = () => {
      setSession(null);
      setSessionExpired(true);
    };
    window.addEventListener(SESSION_EXPIRED_EVENT, onExpired);
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, onExpired);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const response = await loginRequest(email, password);
    saveSession(response);
    setSession(response);
    setSessionExpired(false);
    return response.user;
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setSession(null);
    setSessionExpired(false);
  }, []);

  const value = useMemo(
    () => ({ user: session?.user ?? null, sessionExpired, login, logout }),
    [session, sessionExpired, login, logout],
  );
  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthContextValue {
  const context = use(AuthContext);
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>.');
  return context;
}
