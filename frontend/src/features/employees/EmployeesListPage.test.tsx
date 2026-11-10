import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../../api/employees';
import * as payrollApi from '../../api/payroll';
import { ApiError } from '../../api/problem';
import type { EmployeeListItem, PagedResult } from '../../api/types';
import { AuthProvider } from '../auth/AuthContext';
import { currentLocation, renderRoute, signInAs, type RouteEntry } from '../../test/render';
import { EmployeesListPage } from './EmployeesListPage';

vi.mock('../../api/employees');
vi.mock('../../api/payroll');

function employee(n: number): EmployeeListItem {
  return {
    id: `e${n}`,
    fullName: `Employee ${n}`,
    email: `employee${n}@example.com`,
    jobTitle: 'Engineer',
    department: 'R&D',
    hireDate: '2021-03-01',
    baseMonthlySalary: 4000 + n,
    status: n % 2 === 0 ? 'Inactive' : 'Active',
  };
}

function page(pageNumber: number, totalCount: number): PagedResult<EmployeeListItem> {
  const totalPages = Math.ceil(totalCount / 10);
  const first = (pageNumber - 1) * 10 + 1;
  const count = Math.max(0, Math.min(10, totalCount - first + 1));
  return {
    items: Array.from({ length: count }, (_, i) => employee(first + i)),
    page: pageNumber,
    pageSize: 10,
    totalCount,
    totalPages,
    hasPrevious: pageNumber > 1,
    hasNext: pageNumber < totalPages,
  };
}

const lastQuery = () => vi.mocked(api.listEmployees).mock.lastCall?.[0];

beforeEach(() => {
  signInAs('HR');
  vi.mocked(api.listDepartments).mockResolvedValue(['Finance', 'R&D']);
  vi.mocked(api.listEmployees).mockImplementation(async (query) => page(query.page, 25));
  vi.mocked(payrollApi.getPayrollPolicy).mockResolvedValue({ currency: 'USD' });
});

const renderList = (url: RouteEntry = '/employees') =>
  renderRoute(<EmployeesListPage />, { path: '/employees', url });

describe('EmployeesListPage', () => {
  it('renders the first page with pagination controls', async () => {
    renderList();

    expect(await screen.findByText('Employee 1')).toBeInTheDocument();
    await waitFor(() => expect(document.title).toBe('Employees · Employee Payroll'));
    expect(screen.getByText('25 employees')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '← Previous' })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByRole('button', { name: 'Next →' })).toHaveAttribute('aria-disabled', 'false');
    expect(lastQuery()).toEqual({ page: 1, pageSize: 10, search: undefined, department: undefined, status: undefined });
  });

  it('shows status as text, not color alone', async () => {
    renderList();
    const row = (await screen.findByText('Employee 2')).closest('tr') as HTMLElement;
    expect(within(row).getByText('Inactive')).toBeInTheDocument();
  });

  it('moves to the next page through the URL', async () => {
    const user = userEvent.setup();
    renderList();
    await screen.findByText('Employee 1');

    await user.click(screen.getByRole('button', { name: 'Next →' }));

    expect(currentLocation()).toBe('/employees?page=2');
    expect(await screen.findByText('Employee 11')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();
  });

  it('keeps keyboard focus on the pagination button while the next page loads', async () => {
    const user = userEvent.setup();
    renderList();
    await screen.findByText('Employee 1');

    await user.click(screen.getByRole('button', { name: 'Next →' }));

    expect(screen.getByRole('button', { name: 'Next →' })).toHaveFocus();
    expect(await screen.findByText('Employee 11')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next →' })).toHaveFocus();
  });

  it('shows the notice passed by the form page until the filters change', async () => {
    const user = userEvent.setup();
    renderList({ pathname: '/employees', state: { notice: 'Ada was added.' } });
    expect(await screen.findByText('Ada was added.')).toBeInTheDocument();
    await screen.findByText('Employee 1');

    await user.selectOptions(screen.getByLabelText('Status'), 'Active');

    expect(screen.queryByText('Ada was added.')).not.toBeInTheDocument();
  });

  it('restores filters and page from the URL', async () => {
    renderList('/employees?page=3&status=Active&department=R%26D');

    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next →' })).toHaveAttribute('aria-disabled', 'true');
    expect(lastQuery()).toMatchObject({ page: 3, status: 'Active', department: 'R&D' });
    expect(screen.getByLabelText('Status')).toHaveValue('Active');
  });

  it('resets to page 1 when a filter changes', async () => {
    const user = userEvent.setup();
    renderList('/employees?page=2');
    await screen.findByText('Employee 11');
    await screen.findByRole('option', { name: 'Finance' });

    await user.selectOptions(screen.getByLabelText('Department'), 'Finance');

    expect(currentLocation()).toBe('/employees?department=Finance');
    await waitFor(() => expect(lastQuery()).toMatchObject({ page: 1, department: 'Finance' }));
  });

  it('debounces the search before updating the URL', async () => {
    const user = userEvent.setup();
    renderList();
    await screen.findByText('Employee 1');

    await user.type(screen.getByLabelText('Search'), 'ada');

    expect(currentLocation()).toBe('/employees');
    await waitFor(() => expect(currentLocation()).toBe('/employees?search=ada'));
    await waitFor(() => expect(lastQuery()).toMatchObject({ search: 'ada', page: 1 }));
    expect(vi.mocked(api.listEmployees).mock.calls.filter(([q]) => q.search !== undefined)).toHaveLength(1);
  });

  it('keeps a filter changed while the search debounce is pending', async () => {
    const user = userEvent.setup();
    renderList();
    await screen.findByRole('option', { name: 'Finance' });

    await user.type(screen.getByLabelText('Search'), 'ada');
    await user.selectOptions(screen.getByLabelText('Department'), 'Finance');

    await waitFor(() => expect(currentLocation()).toBe('/employees?department=Finance&search=ada'));
  });

  it('shows salaries in the payroll policy currency', async () => {
    renderList();
    const row = (await screen.findByText('Employee 1')).closest('tr') as HTMLElement;
    await waitFor(() => expect(within(row).getByText('$4,001.00')).toBeInTheDocument());
  });

  it('shows plain salaries when the payroll policy fails to load', async () => {
    vi.mocked(payrollApi.getPayrollPolicy).mockRejectedValue(new ApiError({ status: 500, title: 'Server error' }));
    renderList();
    const row = (await screen.findByText('Employee 1')).closest('tr') as HTMLElement;
    expect(within(row).getByText('4,001.00')).toBeInTheDocument();
  });

  it('passes the employee name to the payslip page', async () => {
    function StateProbe() {
      return <output>{JSON.stringify(useLocation().state)}</output>;
    }
    const user = userEvent.setup();
    render(
      <MemoryRouter initialEntries={['/employees?page=2']}>
        <AuthProvider>
          <Routes>
            <Route path="/employees" element={<EmployeesListPage />} />
            <Route path="/employees/:id/payslip" element={<StateProbe />} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    );

    await user.click(await screen.findByRole('link', { name: 'Payslip for Employee 11' }));

    expect(screen.getByRole('status')).toHaveTextContent('{"from":"?page=2","name":"Employee 11"}');
  });

  it('shows an empty state for filters with no match', async () => {
    vi.mocked(api.listEmployees).mockResolvedValue(page(1, 0));
    renderList('/employees?search=zzz');

    expect(await screen.findByText('No employees match these filters.')).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Pagination' })).not.toBeInTheDocument();
  });

  it('shows an error with a retry', async () => {
    vi.mocked(api.listEmployees).mockRejectedValueOnce(new ApiError({ status: 0, title: "Can't reach the server" }));
    const user = userEvent.setup();
    renderList();

    expect(await screen.findByText("Can't reach the server")).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Employee 1')).toBeInTheDocument();
  });

  it('deletes after confirmation, then refreshes the list', async () => {
    vi.mocked(api.deleteEmployee).mockResolvedValue(undefined);
    const user = userEvent.setup();
    renderList();
    const deleteButton = await screen.findByRole('button', { name: 'Delete Employee 1' });

    await user.click(deleteButton);
    const dialog = screen.getByRole('alertdialog', { name: 'Delete employee?' });
    expect(dialog).toHaveTextContent('Employee 1');
    const callsBefore = vi.mocked(api.listEmployees).mock.calls.length;
    await user.click(within(dialog).getByRole('button', { name: 'Delete employee' }));

    expect(api.deleteEmployee).toHaveBeenCalledWith('e1');
    expect(await screen.findByText('Employee 1 was deleted.')).toBeInTheDocument();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    await waitFor(() => expect(vi.mocked(api.listEmployees).mock.calls.length).toBeGreaterThan(callsBefore));
    // The deleted employee may have been the last one in their department.
    await waitFor(() => expect(api.listDepartments).toHaveBeenCalledTimes(2));
  });

  it('reports "already deleted" on a 404', async () => {
    vi.mocked(api.deleteEmployee).mockRejectedValue(new ApiError({ status: 404, title: 'Not found' }));
    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Delete Employee 3' }));
    await user.click(screen.getByRole('button', { name: 'Delete employee' }));

    expect(await screen.findByText('Employee 3 was already deleted.')).toBeInTheDocument();
  });

  it('keeps the dialog open with the error when the delete fails', async () => {
    vi.mocked(api.deleteEmployee).mockRejectedValue(new ApiError({ status: 500, title: 'Server error' }));
    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Delete Employee 3' }));
    await user.click(screen.getByRole('button', { name: 'Delete employee' }));

    const dialog = screen.getByRole('alertdialog', { name: 'Delete employee?' });
    expect(await within(dialog).findByText('Server error')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Delete employee' })).toBeEnabled();
  });

  it('moves focus to the notice after a delete, since the deleted row disappears', async () => {
    vi.mocked(api.deleteEmployee).mockResolvedValue(undefined);
    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Delete Employee 1' }));
    await user.click(screen.getByRole('button', { name: 'Delete employee' }));

    const notice = await screen.findByText('Employee 1 was deleted.');
    await waitFor(() => expect(notice.closest('[tabindex="-1"]')).toHaveFocus());
  });
});
