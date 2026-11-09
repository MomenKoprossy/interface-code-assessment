import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../../api/employees';
import { ApiError } from '../../api/problem';
import type { EmployeeListItem, PagedResult } from '../../api/types';
import { currentLocation, renderRoute, signInAs } from '../../test/render';
import { EmployeesListPage } from './EmployeesListPage';

vi.mock('../../api/employees');

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
});

const renderList = (url = '/employees') => renderRoute(<EmployeesListPage />, { path: '/employees', url });

describe('EmployeesListPage', () => {
  it('renders the first page with pagination controls', async () => {
    renderList();

    expect(await screen.findByText('Employee 1')).toBeInTheDocument();
    expect(screen.getByText('25 employees')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '← Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next →' })).toBeEnabled();
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

  it('restores filters and page from the URL', async () => {
    renderList('/employees?page=3&status=Active&department=R%26D');

    expect(await screen.findByText('Page 3 of 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Next →' })).toBeDisabled();
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
  });

  it('reports "already deleted" on a 404', async () => {
    vi.mocked(api.deleteEmployee).mockRejectedValue(new ApiError({ status: 404, title: 'Not found' }));
    const user = userEvent.setup();
    renderList();

    await user.click(await screen.findByRole('button', { name: 'Delete Employee 3' }));
    await user.click(screen.getByRole('button', { name: 'Delete employee' }));

    expect(await screen.findByText('Employee 3 was already deleted.')).toBeInTheDocument();
  });
});
