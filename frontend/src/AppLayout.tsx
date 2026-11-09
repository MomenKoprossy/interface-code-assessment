import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/AuthContext';
import { Button } from './shared/components/Button';

export function AppLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function signOut() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <>
      <a href="#main" className="skip-link">
        Skip to main content
      </a>
      <header className="app-header">
        <div className="app-header__inner">
          <span className="app-header__brand">Employee Payroll</span>
          <nav aria-label="Main" className="app-header__nav">
            {user?.role === 'HR' ? <NavLink to="/employees">Employees</NavLink> : <NavLink to="/me/payslip">My payslip</NavLink>}
          </nav>
          {user && (
            <div className="app-header__user">
              <span className="app-header__name">{user.displayName}</span>
              <span className="badge badge--accent">{user.role}</span>
              <Button variant="ghost" small onClick={signOut}>
                Sign out
              </Button>
            </div>
          )}
        </div>
      </header>
      <main id="main" className="app-main" tabIndex={-1}>
        <Outlet />
      </main>
    </>
  );
}
