import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../../api/payroll';
import { ApiError } from '../../api/problem';
import type { Payslip } from '../../api/types';
import { currentMonth } from '../../shared/format';
import { renderRoute, signInAs } from '../../test/render';
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
    const table = screen.getByRole('heading', { name: 'Income tax by bracket' }).nextElementSibling as HTMLElement;
    const rows = within(table).getAllByRole('row').slice(1);

    expect(rows).toHaveLength(4);
    expect(rows[1]).toHaveTextContent('$1,500.00 – $5,000.00');
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

  it('shows a 422 as a friendly notice using the server detail', async () => {
    signInAs('HR');
    vi.mocked(api.getPayslip).mockRejectedValue(
      new ApiError({ status: 422, title: 'Business rule violated', detail: 'Employee was not yet hired in 2019-01.' }),
    );
    renderRoute(<PayslipPage />, { path: '/employees/:id/payslip', url: '/employees/emp-1/payslip?month=2019-01' });

    const detail = await screen.findByText('Employee was not yet hired in 2019-01.');
    const notice = detail.closest('[role="status"]');
    expect(notice).toHaveTextContent('No payslip for this month');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('tells an employee without an employee record that there is nothing to show', () => {
    signInAs('Employee', { employeeId: null });
    renderRoute(<PayslipPage />, { path: '/me/payslip', url: '/me/payslip' });

    expect(screen.getByText('No employee record')).toBeInTheDocument();
    expect(api.getPayslip).not.toHaveBeenCalled();
  });
});
