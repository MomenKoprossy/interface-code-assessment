import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as authApi from './api/auth';
import * as employeesApi from './api/employees';
import * as payrollApi from './api/payroll';
import { App } from './App';
import { ApiError } from './api/problem';
import { expireSession } from './features/auth/session';
import { currentLocation, renderApp, signInAs } from './test/render';

vi.mock('./api/auth');
vi.mock('./api/employees');
vi.mock('./api/payroll');

beforeEach(() => {
  vi.mocked(employeesApi.listEmployees).mockResolvedValue({
    items: [],
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
    hasPrevious: false,
    hasNext: false,
  });
  vi.mocked(employeesApi.listDepartments).mockResolvedValue([]);
  vi.mocked(payrollApi.getPayslip).mockRejectedValue(new ApiError({ status: 422, title: 'x', detail: 'Not available.' }));
});

describe('route guards', () => {
  it('sends an anonymous visitor to the login page', () => {
    renderApp(<App />, '/employees');
    expect(currentLocation()).toBe('/login');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('keeps an Employee out of HR pages and lands them on their payslip', () => {
    signInAs('Employee');
    renderApp(<App />, '/employees/new');
    expect(currentLocation()).toBe('/me/payslip');
  });

  it('keeps HR out of the employee-only page', () => {
    signInAs('HR');
    renderApp(<App />, '/me/payslip');
    expect(currentLocation()).toBe('/employees');
  });

  it.each([
    ['HR', '/employees'],
    ['Employee', '/me/payslip'],
  ] as const)('sends %s from an unknown route to their home page', (role, home) => {
    signInAs(role);
    renderApp(<App />, '/does-not-exist');
    expect(currentLocation()).toBe(home);
  });

  it('shows the user name, role and a sign-out button in the header', async () => {
    const user = userEvent.setup();
    signInAs('HR');
    renderApp(<App />, '/employees');

    expect(screen.getByText('Hannah HR')).toBeInTheDocument();
    expect(screen.getByText('HR', { selector: '.badge' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(currentLocation()).toBe('/login');
    expect(sessionStorage.length).toBe(0);
  });

  it('returns to login when the server rejects the token', async () => {
    signInAs('HR');
    renderApp(<App />, '/employees');
    expect(currentLocation()).toBe('/employees');

    expireSession();

    await waitFor(() => expect(currentLocation()).toBe('/login'));
    expect(screen.getByText('Your session has expired. Please sign in again.')).toBeInTheDocument();
  });
});

describe('login', () => {
  it('signs an HR user in and lands on the employee list', async () => {
    vi.mocked(authApi.login).mockResolvedValue({
      accessToken: 't',
      expiresAtUtc: '2999-01-01T00:00:00Z',
      user: { id: 'u', email: 'hr@example.com', displayName: 'Hannah HR', role: 'HR', employeeId: null, permissions: [] },
    });
    const user = userEvent.setup();
    renderApp(<App />, '/login');

    await user.type(screen.getByLabelText(/email/i), 'hr@example.com');
    await user.type(screen.getByLabelText(/password/i), 'secret');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(currentLocation()).toBe('/employees'));
    expect(authApi.login).toHaveBeenCalledWith('hr@example.com', 'secret');
  });

  it('shows the server message on bad credentials', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new ApiError({ status: 401, title: 'Invalid email or password.' }));
    const user = userEvent.setup();
    renderApp(<App />, '/login');

    await user.type(screen.getByLabelText(/email/i), 'hr@example.com');
    await user.type(screen.getByLabelText(/password/i), 'wrong');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(currentLocation()).toBe('/login');
  });
});
