import { useEffect, useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { errorMessage } from '../../api/problem';
import { Alert } from '../../shared/components/Alert';
import { Button } from '../../shared/components/Button';
import { TextField } from '../../shared/components/TextField';
import { homePathFor, useAuth } from './AuthContext';

export function LoginPage() {
  const { user, login, sessionExpired } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    document.title = 'Sign in · Employee Payroll';
  }, []);

  if (user && !submitting) return <Navigate to={homePathFor(user.role)} replace />;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!email.trim() || !password) {
      setError('Enter your email and password.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const signedIn = await login(email.trim(), password);
      navigate(from ?? homePathFor(signedIn.role), { replace: true });
    } catch (caught) {
      setError(errorMessage(caught));
      setSubmitting(false);
    }
  }

  return (
    <main className="login" id="main">
      <div className="login__card card">
        <h1 className="login__title">Employee Payroll</h1>
        <p className="login__subtitle">Sign in to continue</p>
        {sessionExpired && !error && <Alert tone="info">Your session has expired. Please sign in again.</Alert>}
        {error && <Alert>{error}</Alert>}
        <form onSubmit={handleSubmit} noValidate className="stack">
          <TextField
            label="Email"
            type="email"
            autoComplete="username"
            maxLength={254}
            value={email}
            onChange={setEmail}
            required
          />
          <TextField
            label="Password"
            type="password"
            autoComplete="current-password"
            maxLength={128}
            value={password}
            onChange={setPassword}
            required
          />
          <Button type="submit" disabled={submitting} className="button--block">
            {submitting ? 'Signing in…' : 'Sign in'}
          </Button>
        </form>
      </div>
    </main>
  );
}
