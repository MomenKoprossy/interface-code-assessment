import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../../api/payroll';
import { ApiError } from '../../api/problem';
import type { Payslip } from '../../api/types';
import { currentMonth } from '../../shared/format';
import { currentLocation, renderRoute, signInAs } from '../../test/render';
import { PayslipPage } from './PayslipPage';
import { PayslipView } from './PayslipView';

vi.mock('../../api/payroll');

/** As the server would return it for the default policy (USD, SI 11 % capped at 1,100). */
const payslip: Payslip = {
  employeeId: 'emp-1',
  employeeName: 'Ada Lovelace',
  department: 'R&D',
  jobTitle: 'Principal Engineer',
  month: '2026-11',
  currency: 'USD',
  baseSalary: 12000,
  allowances: [{ name: 'Housing', amount: 1000 }],
  totalAllowances: 1000,
  grossPay: 13000,
  socialInsurance: { rate: 0.11, monthlyCap: 1100, uncappedAmount: 1320, amount: 1100, capApplied: true },
  taxableIncome: 11900,
  incomeTax: {
    amount: 1825,
    brackets: [
      { from: 0, upTo: 1500, rate: 0, taxedAmount: 1500, tax: 0 },
      { from: 1500, upTo: 5000, rate: 0.1, taxedAmount: 3500, tax: 350 },
      { from: 5000, upTo: 10000, rate: 0.2, taxedAmount: 5000, tax: 1000 },
      { from: 10000, upTo: null, rate: 0.25, taxedAmount: 1900, tax: 475 },
    ],
  },
  otherDeductions: [{ name: 'Loan repayment', amount: 200 }],
  totalOtherDeductions: 200,
  totalDeductions: 3125,
  netPay: 9875,
  roundingRule: 'Each line is rounded to 2 decimals, half away from zero.',
};

const rowValue = (label: string | RegExp) => {
  const row = screen.getByRole('rowheader', { name: label }).closest('tr') as HTMLElement;
  return within(row).getByRole('cell').textContent;
};

describe('PayslipView', () => {
  it('renders earnings, deductions and net pay exactly as the server sent them', () => {
    render(<PayslipView payslip={payslip} />);

    expect(screen.getByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument();
    expect(screen.getByText('November 2026')).toBeInTheDocument();
    expect(rowValue('Base salary')).toBe('$12,000.00');
    expect(rowValue('Housing')).toBe('$1,000.00');
    expect(rowValue('Gross pay')).toBe('$13,000.00');
    expect(rowValue(/Income tax/)).toBe('$1,825.00');
    expect(rowValue('Loan repayment')).toBe('$200.00');
    expect(rowValue('Total deductions')).toBe('$3,125.00');
    expect(rowValue('Net pay')).toBe('$9,875.00');
  });

  it('explains the social insurance cap', () => {
    render(<PayslipView payslip={payslip} />);
    expect(rowValue(/Social insurance \(11% of base salary\)/)).toBe('$1,100.00');
    expect(screen.getByText('Capped at $1,100.00; uncapped it would be $1,320.00.')).toBeInTheDocument();
  });

  it('omits the cap note when the cap did not apply', () => {
    const uncapped = { ...payslip, socialInsurance: { ...payslip.socialInsurance, capApplied: false } };
    render(<PayslipView payslip={uncapped} />);
    expect(screen.queryByText(/Capped at/)).not.toBeInTheDocument();
  });

  it('lists tax per bracket, including the open-ended top bracket', () => {
    render(<PayslipView payslip={payslip} />);
    const table = screen.getByRole('table', { name: 'Income tax by bracket' });
    const rows = within(table).getAllByRole('row').slice(1);

    expect(rows).toHaveLength(4);
    // Upper bounds are inclusive, so a bound is never repeated as the start of the next bracket.
    expect(rows[0]).toHaveTextContent('Up to $1,500.00');
    expect(rows[1]).toHaveTextContent('Over $1,500.00 up to $5,000.00');
    expect(rows[1]).toHaveTextContent('10%');
    expect(rows[1]).toHaveTextContent('$350.00');
    expect(rows[3]).toHaveTextContent('Over $10,000.00');
    expect(rows[3]).toHaveTextContent('25%');
  });
});

describe('PayslipPage', () => {
  beforeEach(() => {
    vi.mocked(api.getPayslip).mockResolvedValue(payslip);
  });

  it("loads the signed-in employee's own payslip for the current month", async () => {
    signInAs('Employee', { employeeId: 'emp-1' });
    renderRoute(<PayslipPage />, { path: '/me/payslip', url: '/me/payslip' });

    expect(await screen.findByText('$9,875.00')).toBeInTheDocument();
    expect(api.getPayslip).toHaveBeenCalledWith('emp-1', currentMonth(), expect.any(AbortSignal));
    expect(screen.getByLabelText('Month')).toHaveValue(currentMonth());
  });

  it('loads the employee from the route for HR and reloads when the month changes', async () => {
    signInAs('HR');
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-9/payslip?month=2026-03' });
    await screen.findByText('$9,875.00');
    expect(api.getPayslip).toHaveBeenLastCalledWith('emp-9', '2026-03', expect.any(AbortSignal));

    // Month inputs reject partial text in jsdom, so set the whole value at once.
    fireEvent.change(screen.getByLabelText('Month'), { target: { value: '2026-04' } });

    await waitFor(() => expect(api.getPayslip).toHaveBeenLastCalledWith('emp-9', '2026-04', expect.any(AbortSignal)));
  });

  it('does not snap a half-typed month back while typing (text fallback without a native picker)', async () => {
    signInAs('HR');
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-9/payslip?month=2026-03' });
    await screen.findByText('$9,875.00');
    // jsdom sanitizes partial values of type="month" to '', so simulate the text-box fallback browsers render.
    const input = screen.getByLabelText('Month');
    input.setAttribute('type', 'text');
    const user = userEvent.setup();

    await user.clear(input);
    await user.type(input, '2025-03');

    expect(input).toHaveValue('2025-03');
    await waitFor(() => expect(api.getPayslip).toHaveBeenLastCalledWith('emp-9', '2025-03', expect.any(AbortSignal)));
    expect(api.getPayslip).not.toHaveBeenCalledWith('emp-9', currentMonth(), expect.any(AbortSignal));
  });

  it('shows the server field message for a rejected month instead of a generic title', async () => {
    signInAs('HR');
    vi.mocked(api.getPayslip).mockRejectedValue(
      new ApiError({ status: 400, title: 'Validation failed', fieldErrors: { month: ['Month must be between 1900-01 and 9999-12.'] } }),
    );
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip?month=1800-01' });

    expect(await screen.findByRole('alert')).toHaveTextContent('Month must be between 1900-01 and 9999-12.');
    expect(screen.getByLabelText('Month')).toHaveAttribute('min', '1900-01');
  });

  it('shows a 422 as a friendly notice using the server detail', async () => {
    signInAs('HR');
    vi.mocked(api.getPayslip).mockRejectedValue(
      new ApiError({ status: 422, title: 'Business rule violated', detail: 'Employee was not yet hired in 2019-01.' }),
    );
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip?month=2019-01' });

    const detail = await screen.findByText('Employee was not yet hired in January 2019.');
    const notice = detail.closest('[role="status"]');
    expect(notice).toHaveTextContent('No payslip available');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it("names whose payslip it is from the payload, else from the list link's state, else not at all", async () => {
    signInAs('HR');
    const { unmount } = renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip' });
    expect(await screen.findByRole('heading', { level: 1, name: 'Payslip: Ada Lovelace' })).toBeInTheDocument();
    unmount();

    vi.mocked(api.getPayslip).mockRejectedValue(
      new ApiError({ status: 422, title: 'Business rule violated', detail: 'Employee was not yet hired in 2019-01.' }),
    );
    const { unmount: unmountLinked } = renderRoute(<PayslipPage />, {
      path: '/employees/:id/payslip',
      url: { pathname: '/employees/emp-1/payslip', search: '?month=2019-01', state: { from: '?page=2', name: 'Grace Hopper' } },
    });
    await screen.findByText('Employee was not yet hired in January 2019.');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Payslip: Grace Hopper');
    expect(screen.getByRole('link', { name: '← Back to employees' })).toHaveAttribute('href', '/employees?page=2');
    unmountLinked();

    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip?month=2019-01' });
    await screen.findByText('Employee was not yet hired in January 2019.');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/^Payslip$/);
  });

  it("keeps the employee's name after switching to a month with no payslip", async () => {
    signInAs('HR');
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip?month=2026-03' });
    expect(await screen.findByRole('heading', { level: 1, name: 'Payslip: Ada Lovelace' })).toBeInTheDocument();

    vi.mocked(api.getPayslip).mockRejectedValue(
      new ApiError({ status: 422, title: 'Business rule violated', detail: 'Employee was not yet hired in 2019-01.' }),
    );
    fireEvent.change(screen.getByLabelText('Month'), { target: { value: '2019-01' } });

    await screen.findByText('Employee was not yet hired in January 2019.');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Payslip: Ada Lovelace');
  });

  it('treats a month the API would reject (before 1900) like any malformed month: shows the current month', async () => {
    signInAs('HR');
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip?month=0000-01' });

    await screen.findByText('$9,875.00');
    expect(api.getPayslip).toHaveBeenLastCalledWith('emp-1', currentMonth(), expect.any(AbortSignal));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    // The URL is corrected to the month shown, so it can be shared as is.
    await waitFor(() => expect(currentLocation()).toBe(`/employees/emp-1/payslip?month=${currentMonth()}`));
  });

  it('tells an employee without an employee record that there is nothing to show', () => {
    signInAs('Employee', { employeeId: null });
    renderRoute(<PayslipPage />, { path: '/me/payslip', url: '/me/payslip' });

    expect(screen.getByText('No employee record')).toBeInTheDocument();
    expect(api.getPayslip).not.toHaveBeenCalled();
  });
});
