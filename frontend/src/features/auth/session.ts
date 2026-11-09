import type { LoginResponse } from '../../api/types';

// Lives outside React so the HTTP client can read the token and clear the session on a 401.
const STORAGE_KEY = 'employee-payroll.session';
export const SESSION_EXPIRED_EVENT = 'employee-payroll:session-expired';

export type Session = LoginResponse;

export function loadSession(now: Date = new Date()): Session | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    if (!session.accessToken || Date.parse(session.expiresAtUtc) <= now.getTime()) {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
    return session;
  } catch {
    return null;
  }
}

export function saveSession(session: Session): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}

export function getAccessToken(): string | null {
  return loadSession()?.accessToken ?? null;
}

/** Called by the HTTP client when the server rejects our token; AuthProvider listens and drops the user. */
export function expireSession(): void {
  clearSession();
  window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT));
}
