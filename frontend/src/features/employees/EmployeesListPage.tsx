import { Fragment, useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { getPayrollPolicy } from '../../api/payroll';
import { errorMessage } from '../../api/problem';
import type { EmployeeListItem } from '../../api/types';
import { Alert } from '../../shared/components/Alert';
import { Button, buttonClass } from '../../shared/components/Button';
import { PageHeader } from '../../shared/components/PageHeader';
import { Pagination } from '../../shared/components/Pagination';
import { SelectField } from '../../shared/components/SelectField';
import { Spinner } from '../../shared/components/Spinner';
import { TextField } from '../../shared/components/TextField';
import { formatDate, formatMoney } from '../../shared/format';
import { useAsync } from '../../shared/useAsync';
import { DeleteEmployeeDialog, type DeleteOutcome } from './DeleteEmployeeDialog';
import { parseListQuery, updateListParams, useDepartments, useEmployees } from './useEmployees';

export const SEARCH_DEBOUNCE_MS = 300;

const statusOptions = [
  { value: '', label: 'All statuses' },
  { value: 'Active', label: 'Active' },
  { value: 'Inactive', label: 'Inactive' },
];

export function EmployeesListPage() {
  const [params, setParams] = useSearchParams();
  const query = parseListQuery(params);
  const employees = useEmployees(query);
  const departments = useDepartments();
  // async wrapper: other pages' tests auto-mock api/payroll, where getPayrollPolicy returns undefined, not a promise.
  const policy = useAsync('payroll-policy', async (signal) => getPayrollPolicy(signal));
  const location = useLocation();
  const navigate = useNavigate();
  const [notice, setNotice] = useState<string | null>((location.state as { notice?: string } | null)?.notice ?? null);
  const noticeRef = useRef<HTMLDivElement>(null);
  const [toDelete, setToDelete] = useState<EmployeeListItem | null>(null);

  const urlSearch = query.search ?? '';
  const [searchText, setSearchText] = useState(urlSearch);
  const [syncedUrlSearch, setSyncedUrlSearch] = useState(urlSearch);
  // Back/forward navigation changes the URL under us: mirror it into the box unless it's our own debounced write.
  if (urlSearch !== syncedUrlSearch) {
    setSyncedUrlSearch(urlSearch);
    if (urlSearch !== searchText.trim()) setSearchText(urlSearch);
  }

  // The notice was read once; clear it from history so a refresh or Back doesn't show it again.
  useEffect(() => {
    if (location.state) navigate({ search: location.search }, { replace: true, state: null });
  }, [location.state, location.search, navigate]);

  // Focus the notice so it is announced, and so focus isn't lost when the deleted row disappears.
  useEffect(() => {
    if (notice) noticeRef.current?.focus();
  }, [notice]);

  // The debounced write runs after later renders; setParams' updater would see this render's stale params.
  const paramsRef = useRef(params);
  useEffect(() => {
    paramsRef.current = params;
  }, [params]);
  const debounceTimer = useRef<number | undefined>(undefined);
  useEffect(() => () => window.clearTimeout(debounceTimer.current), []);

  function changeSearch(value: string) {
    setSearchText(value);
    setNotice(null);
    window.clearTimeout(debounceTimer.current);
    debounceTimer.current = window.setTimeout(
      () => setParams(() => updateListParams(paramsRef.current, { search: value.trim() })),
      SEARCH_DEBOUNCE_MS,
    );
  }

  function setFilter(name: 'department' | 'status', value: string) {
    setNotice(null);
    setParams((current) => updateListParams(current, { [name]: value }));
  }

  function goToPage(page: number) {
    setNotice(null);
    setParams((current) => updateListParams(current, { page: String(page) }));
  }

  function handleDeleteDone(outcome: DeleteOutcome) {
    const name = toDelete?.fullName ?? 'The employee';
    setNotice(outcome === 'deleted' ? `${name} was deleted.` : `${name} was already deleted.`);
    setToDelete(null);
    employees.reload();
    departments.reload();
  }

  const departmentOptions = [
    { value: '', label: 'All departments' },
    ...[...new Set([...(departments.data ?? []), ...(query.department ? [query.department] : [])])].map((d) => ({
      value: d,
      label: d,
    })),
  ];
  const hasFilters = Boolean(query.search || query.department || query.status);
  const result = employees.data;

  return (
    <>
      <PageHeader
        title="Employees"
        subtitle={
          result ? (
            <span role="status">
              {result.totalCount} {result.totalCount === 1 ? 'employee' : 'employees'}
            </span>
          ) : undefined
        }
        actions={
          <Link to="/employees/new" state={{ from: location.search }} className={buttonClass('primary')}>
            <span aria-hidden="true">+</span> Add employee
          </Link>
        }
      />

      {notice && (
        <div ref={noticeRef} tabIndex={-1} className="notice">
          <Alert tone="success">{notice}</Alert>
        </div>
      )}

      <section className="filters card" aria-label="Filters">
        <TextField
          label="Search"
          type="search"
          placeholder="Name or email"
          spellCheck={false}
          value={searchText}
          onChange={changeSearch}
          className="filters__search"
        />
        <SelectField
          label="Department"
          value={query.department ?? ''}
          options={departmentOptions}
          onChange={(value) => setFilter('department', value)}
        />
        <SelectField
          label="Status"
          value={query.status ?? ''}
          options={statusOptions}
          onChange={(value) => setFilter('status', value)}
        />
      </section>

      {employees.error !== undefined && (
        <Alert title="Couldn't load employees">
          <p>{errorMessage(employees.error)}</p>
          <Button variant="secondary" small onClick={employees.reload}>
            Try again
          </Button>
        </Alert>
      )}
      {/* While a page or reload loads, the previous rows stay mounted so the focused control isn't lost. */}
      {employees.loading && !result && <Spinner label="Loading employees…" />}

      {result && result.items.length === 0 && (
        <div className="empty-state card">
          {result.totalCount > 0 && query.page > result.totalPages ? (
            <>
              <p>This page is empty.</p>
              <Button variant="secondary" onClick={() => goToPage(result.totalPages)}>
                Go to the last page
              </Button>
            </>
          ) : hasFilters ? (
            <p>No employees match these filters.</p>
          ) : (
            <p>
              No employees yet. <Link to="/employees/new">Add the first one</Link>.
            </p>
          )}
        </div>
      )}

      {result && result.items.length > 0 && (
        <>
          <EmployeeTable
            items={result.items}
            busy={employees.loading}
            currency={policy.data?.currency}
            onDelete={setToDelete}
          />
          <Pagination page={result.page} totalPages={result.totalPages} onPageChange={goToPage} />
        </>
      )}

      {toDelete && (
        <DeleteEmployeeDialog employee={toDelete} onClose={() => setToDelete(null)} onDone={handleDeleteDone} />
      )}
    </>
  );
}

interface EmployeeTableProps {
  items: EmployeeListItem[];
  busy: boolean;
  currency?: string;
  onDelete: (employee: EmployeeListItem) => void;
}

function EmployeeTable({ items, busy, currency, onDelete }: EmployeeTableProps) {
  // The edit and payslip pages link back to the list with its filters and page intact.
  const from = useLocation().search;
  return (
    <table className="data-table" aria-busy={busy}>
      <caption className="visually-hidden">Employees</caption>
      <thead>
        <tr>
          <th scope="col">Name</th>
          <th scope="col">Job title</th>
          <th scope="col">Department</th>
          <th scope="col">Hire date</th>
          <th scope="col" className="data-table__number">
            Base salary
          </th>
          <th scope="col">Status</th>
          <th scope="col">
            <span className="visually-hidden">Actions</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {items.map((employee) => (
          <tr key={employee.id}>
            <td>
              <span className="data-table__primary">{employee.fullName}</span>
              <span className="data-table__secondary">
                {/* Line-break opportunities before "@" and ".", so a long email wraps there, never mid-word. */}
                {employee.email.split(/(?=[@.])/).map((part, i) => (
                  <Fragment key={i}>
                    {i > 0 && <wbr />}
                    {part}
                  </Fragment>
                ))}
              </span>
            </td>
            <td data-label="Job title">{employee.jobTitle}</td>
            <td data-label="Department">{employee.department}</td>
            <td data-label="Hire date" className="data-table__nowrap">
              {formatDate(employee.hireDate)}
            </td>
            <td data-label="Base salary" className="data-table__number">
              {formatMoney(employee.baseMonthlySalary, currency)}
            </td>
            <td data-label="Status">
              <StatusBadge status={employee.status} />
            </td>
            <td className="data-table__actions-cell">
              <div className="data-table__actions">
                <Link
                  to={`/employees/${employee.id}/edit`}
                  state={{ from }}
                  className={buttonClass('secondary', true)}
                  aria-label={`Edit ${employee.fullName}`}
                >
                  Edit
                </Link>
                <Link
                  to={`/employees/${employee.id}/payslip`}
                  state={{ from, name: employee.fullName }}
                  className={buttonClass('secondary', true)}
                  aria-label={`Payslip for ${employee.fullName}`}
                >
                  Payslip
                </Link>
                <Button
                  variant="ghost"
                  small
                  onClick={() => onDelete(employee)}
                  aria-label={`Delete ${employee.fullName}`}
                >
                  Delete
                </Button>
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function StatusBadge({ status }: { status: EmployeeListItem['status'] }) {
  return (
    <span className={`badge badge--${status === 'Active' ? 'success' : 'muted'}`}>
      <span className="badge__dot" aria-hidden="true" /> {status}
    </span>
  );
}
