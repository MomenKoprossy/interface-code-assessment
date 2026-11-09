import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import type { CurrentUser, Role } from '../api/types';
import { AuthProvider } from '../features/auth/AuthContext';
import { saveSession } from '../features/auth/session';

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
}

export const currentLocation = () => document.querySelector('[data-testid="location"]')?.textContent;

export function signInAs(role: Role, overrides: Partial<CurrentUser> = {}): CurrentUser {
  const user: CurrentUser = {
    id: 'user-1',
    email: role === 'HR' ? 'hr@example.com' : 'ada@example.com',
    displayName: role === 'HR' ? 'Hannah HR' : 'Ada Lovelace',
    role,
    employeeId: role === 'Employee' ? 'emp-1' : null,
    permissions: [],
    ...overrides,
  };
  saveSession({ accessToken: 'test-token', expiresAtUtc: '2999-01-01T00:00:00Z', user });
  return user;
}

/** Renders `element` at `path` (a route pattern) inside a router + auth provider, starting at `url`. */
export function renderRoute(element: ReactElement, { path = '/', url = '/' }: { path?: string; url?: string } = {}) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <AuthProvider>
        <Routes>
          <Route path={path} element={element} />
          <Route path="*" element={null} />
        </Routes>
        <LocationProbe />
      </AuthProvider>
    </MemoryRouter>,
  );
}

/** Renders the whole route table (App) at `url`. */
export function renderApp(app: ReactElement, url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <AuthProvider>
        {app}
        <LocationProbe />
      </AuthProvider>
    </MemoryRouter>,
  );
}
