import { useEffect, useRef, useState, type FormEvent } from 'react';
import { flushSync } from 'react-dom';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { errorMessage, isStatus } from '../../api/problem';
import { Alert } from '../../shared/components/Alert';
import { Button } from '../../shared/components/Button';
import { TextField } from '../../shared/components/TextField';
import { validateEmail } from '../employees/validation';
import { homePathFor, useAuth } from './AuthContext';

type FieldErrors = { email?: string; password?: string };

export function LoginPage() {
  const { user, login, sessionExpired } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from;

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const formRef = useRef<HTMLFormElement>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    document.title = 'Sign in · Employee Payroll';
  }, []);

  if (user && !submitting) return <Navigate to={homePathFor(user.role)} replace />;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const invalid = { email: validateEmail(email), password: password ? undefined : 'Password is required.' };
    setError(null);
    if (invalid.email || invalid.password) {
      showFieldErrors(invalid);
      return;
    }
    setFieldErrors({});
    setSubmitting(true);
    try {
      const signedIn = await login(email.trim(), password);
      navigate(from ?? homePathFor(signedIn.role), { replace: true });
    } catch (caught) {
      const server = isStatus(caught, 400)
        ? { email: caught.fieldErrors.email?.[0], password: caught.fieldErrors.password?.[0] }
        : {};
      setSubmitting(false);
      if (server.email || server.password) showFieldErrors(server);
      else {
        setFieldErrors({});
        setError(errorMessage(caught));
      }
    }
  }

  /** Same as the employee form: after a failed submit, focus moves to the first invalid field. */
  function showFieldErrors(errors: FieldErrors) {
    flushSync(() => setFieldErrors(errors));
    formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
  }

  return (
    <main className="login" id="main">
      <div className="login__card card">
        <h1 className="login__title">Employee Payroll</h1>
        <p className="login__subtitle">Sign in to continue</p>
        {sessionExpired && !error && <Alert tone="info">Your session has expired. Please sign in again.</Alert>}
        {error && <Alert>{error}</Alert>}
        <form ref={formRef} onSubmit={handleSubmit} noValidate className="stack">
          <TextField
            label="Email"
            type="email"
            autoComplete="username"
            maxLength={254}
            value={email}
            onChange={setEmail}
            error={fieldErrors.email}
            required
          />
          <TextField
            label="Password"
            type="password"
            autoComplete="current-password"
            maxLength={128}
            value={password}
            onChange={setPassword}
            error={fieldErrors.password}
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
