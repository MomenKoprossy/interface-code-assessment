import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../../api/employees';
import { ApiError } from '../../api/problem';
import type { EmployeeDetails } from '../../api/types';
import { App } from '../../App';
import { currentLocation, renderApp, signInAs } from '../../test/render';

vi.mock('../../api/employees');
vi.mock('../../api/payroll');

const ada: EmployeeDetails = {
  id: 'e1',
  fullName: 'Ada Lovelace',
  email: 'ada@example.com',
  jobTitle: 'Engineer',
  department: 'R&D',
  hireDate: '2021-03-01',
  baseMonthlySalary: 5000,
  status: 'Active',
  allowances: [{ name: 'Housing', amount: 500 }],
  deductions: [],
};

beforeEach(() => {
  signInAs('HR');
  vi.mocked(api.listDepartments).mockResolvedValue(['R&D']);
  vi.mocked(api.listEmployees).mockImplementation(async (query) => ({
    items: [ada],
    page: query.page,
    pageSize: 10,
    totalCount: 11,
    totalPages: 2,
    hasPrevious: query.page > 1,
    hasNext: query.page < 2,
  }));
  vi.mocked(api.getEmployee).mockResolvedValue(ada);
  vi.mocked(api.updateEmployee).mockImplementation(async (id, input) => ({ ...input, id }));
  vi.mocked(api.createEmployee).mockImplementation(async (input) => ({ ...input, id: 'new' }));
});

describe('EmployeeFormPage', () => {
  it('preloads the employee, including pay items, for editing', async () => {
    renderApp(<App />, '/employees/e1/edit');

    expect(await screen.findByLabelText(/full name/i)).toHaveValue('Ada Lovelace');
    expect(screen.getByRole('textbox', { name: 'Allowance 1 name' })).toHaveValue('Housing');
    expect(screen.getByRole('textbox', { name: 'Allowance 1 amount' })).toHaveValue('500.00');
    expect(screen.getByText(/Amount \(monthly\)/)).toBeVisible();
    expect(api.getEmployee).toHaveBeenCalledWith('e1', expect.any(AbortSignal));
    await waitFor(() => expect(document.title).toBe('Edit Ada Lovelace · Employee Payroll'));
  });

  it('shows "Employee not found" on a 404', async () => {
    vi.mocked(api.getEmployee).mockRejectedValue(new ApiError({ status: 404, title: 'Not found' }));
    renderApp(<App />, '/employees/gone/edit');

    expect(await screen.findByText('Employee not found')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save changes' })).not.toBeInTheDocument();
  });

  it('updates from a filtered list and returns to the same filters and page with a notice', async () => {
    const user = userEvent.setup();
    renderApp(<App />, '/employees?page=2&status=Active');

    await user.click(await screen.findByRole('link', { name: 'Edit Ada Lovelace' }));
    const jobTitle = await screen.findByLabelText(/job title/i);
    expect(screen.getByRole('link', { name: 'Cancel' })).toHaveAttribute('href', '/employees?page=2&status=Active');
    expect(screen.getByRole('link', { name: 'Back to employees' })).toHaveAttribute(
      'href',
      '/employees?page=2&status=Active',
    );

    await user.clear(jobTitle);
    await user.type(jobTitle, 'Architect');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(currentLocation()).toBe('/employees?page=2&status=Active'));
    expect(api.updateEmployee).toHaveBeenCalledWith('e1', expect.objectContaining({ jobTitle: 'Architect' }));
    expect(api.createEmployee).not.toHaveBeenCalled();
    expect(await screen.findByText('Ada Lovelace was updated.')).toBeInTheDocument();
  });

  it('creates a new employee and lands on the list', async () => {
    const user = userEvent.setup();
    renderApp(<App />, '/employees/new');

    await user.type(screen.getByLabelText(/full name/i), 'Grace Hopper');
    await user.type(screen.getByLabelText(/^email/i), 'grace@example.com');
    await user.type(screen.getByLabelText(/job title/i), 'Admiral');
    await user.type(screen.getByLabelText(/department/i), 'Navy');
    fireEvent.change(screen.getByLabelText(/hire date/i), { target: { value: '2020-01-15' } });
    await user.type(screen.getByLabelText(/base monthly salary/i), '7000');
    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    await waitFor(() => expect(currentLocation()).toBe('/employees'));
    expect(api.createEmployee).toHaveBeenCalledWith(expect.objectContaining({ fullName: 'Grace Hopper', baseMonthlySalary: 7000 }));
    expect(api.updateEmployee).not.toHaveBeenCalled();
    expect(await screen.findByText('Grace Hopper was added.')).toBeInTheDocument();
  });
});
