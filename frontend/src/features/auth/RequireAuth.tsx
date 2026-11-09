import { Navigate, Outlet, useLocation } from 'react-router-dom';
import type { Role } from '../../api/types';
import { homePathFor, useAuth } from './AuthContext';

/** UX-only guards: they decide what to render. The server enforces every permission. */
export function RequireAuth() {
  const { user } = useAuth();
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return <Outlet />;
}

export function RequireRole({ role }: { role: Role }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (user.role !== role) return <Navigate to={homePathFor(user.role)} replace />;
  return <Outlet />;
}

/** Sends any unknown URL to the signed-in user's home page (or to login). */
export function HomeRedirect() {
  const { user } = useAuth();
  return <Navigate to={user ? homePathFor(user.role) : '/login'} replace />;
}
