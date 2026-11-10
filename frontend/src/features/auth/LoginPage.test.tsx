import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import * as authApi from '../../api/auth';
import { ApiError } from '../../api/problem';
import { renderRoute } from '../../test/render';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth');

const fields = () => ({ email: screen.getByLabelText(/email/i), password: screen.getByLabelText(/password/i) });

async function submit(email: string, password: string) {
  const user = userEvent.setup();
  renderRoute(<LoginPage />, { path: '/login', url: '/login' });
  if (email) await user.type(fields().email, email);
  if (password) await user.type(fields().password, password);
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  it('shows required errors on the fields and does not call the server', async () => {
    await submit('', '');

    expect(fields().email).toHaveAccessibleDescription('Email is required.');
    expect(fields().password).toHaveAccessibleDescription('Password is required.');
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('checks the email format on the client', async () => {
    await submit('not-an-email', 'secret');

    expect(fields().email).toHaveAttribute('aria-invalid', 'true');
    expect(fields().email).toHaveAccessibleDescription(/valid email address/);
    expect(fields().password).not.toHaveAttribute('aria-invalid');
    expect(authApi.login).not.toHaveBeenCalled();
  });

  it('puts server 400 field errors on the fields instead of a generic alert', async () => {
    vi.mocked(authApi.login).mockRejectedValue(
      new ApiError({ status: 400, title: 'Validation failed', fieldErrors: { password: ['Password must be at most 128 characters.'] } }),
    );
    await submit('hr@example.com', 'secret');

    expect(await screen.findByText('Password must be at most 128 characters.')).toBeInTheDocument();
    expect(fields().password).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByText('Validation failed')).not.toBeInTheDocument();
  });

  it('keeps bad credentials in the top alert without blaming either field', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new ApiError({ status: 401, title: 'Invalid email or password.' }));
    await submit('hr@example.com', 'wrong');

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(fields().email).not.toHaveAttribute('aria-invalid');
    expect(fields().password).not.toHaveAttribute('aria-invalid');
  });
});
