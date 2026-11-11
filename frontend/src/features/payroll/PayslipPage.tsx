import { useEffect, useId, useState } from 'react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { getEmployee } from '../../api/employees';
import { errorMessage, isStatus } from '../../api/problem';
import { Alert } from '../../shared/components/Alert';
import { PageHeader } from '../../shared/components/PageHeader';
import { Spinner } from '../../shared/components/Spinner';
import { currentMonth, formatMonth } from '../../shared/format';
import { useAsync } from '../../shared/useAsync';
import { useAuth } from '../auth/AuthContext';
import { PayslipView } from './PayslipView';
import { MONTH_PATTERN, usePayslip } from './usePayslip';

/** HR: `/employees/:id/payslip` for anyone. Employee: `/me/payslip` for their own record. */
export function PayslipPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const monthInputId = useId();
  const location = useLocation();
  const linkState = location.state as { from?: string; name?: unknown } | null;
  const listUrl = `/employees${linkState?.from ?? ''}`;

  const employeeId = id ?? user?.employeeId ?? null;
  const requested = params.get('month') ?? '';
  const month = MONTH_PATTERN.test(requested) ? requested : currentMonth();
  const payslip = usePayslip(employeeId, month);
  // A month with no payslip (422) still says whose page this is: the last loaded name, else the one the list link passed.
  const [known, setKnown] = useState<{ id: string | null; name: string }>();
  if (payslip.data && (known?.id !== employeeId || known.name !== payslip.data.employeeName)) {
    setKnown({ id: employeeId, name: payslip.data.employeeName });
  }
  const knownName =
    payslip.data?.employeeName ??
    (known?.id === employeeId ? known.name : undefined) ??
    (typeof linkState?.name === 'string' ? linkState.name : undefined);
  // Last resort (HR opened a link straight to a month with no payslip): ask for the employee's name.
  const lookup = useAsync(id && !knownName && payslip.error !== undefined ? `employee-name:${id}` : null, async (signal) =>
    getEmployee(id ?? '', signal),
  );
  const name = knownName ?? lookup.data?.fullName;

  // The input keeps its own draft so a half-typed month (text fallback where there is no native picker) isn't
  // replaced on every keystroke; only a complete month reaches the URL. Back/forward still syncs the draft.
  const [draft, setDraft] = useState(month);
  const [syncedMonth, setSyncedMonth] = useState(month);
  if (month !== syncedMonth) {
    setSyncedMonth(month);
    setDraft(month);
  }

  // A malformed ?month= falls back to the current month; make the URL say so too, so it can be shared as shown.
  useEffect(() => {
    if (params.has('month') && requested !== month) setParams({ month }, { replace: true, state: location.state });
  }, [params, requested, month, setParams, location.state]);

  const changeMonth = (value: string) => {
    setDraft(value);
    if (MONTH_PATTERN.test(value)) setParams({ month: value }, { replace: true, state: location.state });
  };

  return (
    <>
      <PageHeader
        title={id ? (name ? `Payslip: ${name}` : 'Payslip') : 'My payslip'}
        subtitle={id ? <Link to={listUrl}>
              <span aria-hidden="true">←</span> Back to employees
            </Link> : undefined}
      />

      <div className="card payslip-toolbar">
        <label htmlFor={monthInputId} className="field__label">
          Month
        </label>
        <input
          id={monthInputId}
          type="month"
          className="field__control payslip-toolbar__month"
          min="1900-01"
          placeholder="YYYY-MM"
          value={draft}
          onChange={(event) => changeMonth(event.target.value)}
        />
      </div>

      {!employeeId && (
        <Alert tone="info" title="No employee record">
          <p>Your account isn't linked to an employee record, so there is no payslip to show.</p>
        </Alert>
      )}
      {payslip.loading && <Spinner label={`Loading payslip for ${formatMonth(month)}…`} />}
      {payslip.error !== undefined && <PayslipError error={payslip.error} month={month} />}
      {/* Never show the previous month's payslip while the next one loads. */}
      {payslip.data && !payslip.loading && <PayslipView payslip={payslip.data} />}
    </>
  );
}

function PayslipError({ error, month }: { error: unknown; month: string }) {
  // 422 is an expected business outcome (inactive, not yet hired, deductions exceed gross), not a failure.
  if (isStatus(error, 422)) {
    return (
      <Alert tone="info" title="No payslip available">
        {/* The server names the month as YYYY-MM; show it the way the rest of the page does. */}
        <p>{errorMessage(error).replace(month, formatMonth(month))}</p>
      </Alert>
    );
  }
  if (isStatus(error, 404)) return <Alert title="Employee not found">It may have been deleted.</Alert>;
  if (isStatus(error, 403)) return <Alert title="Not allowed">You can only view your own payslip.</Alert>;
  return <Alert title="Couldn't load the payslip">{errorMessage(error)}</Alert>;
}
