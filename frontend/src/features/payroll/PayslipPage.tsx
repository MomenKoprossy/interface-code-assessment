import { useId } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { errorMessage, isStatus } from '../../api/problem';
import { Alert } from '../../shared/components/Alert';
import { PageHeader } from '../../shared/components/PageHeader';
import { Spinner } from '../../shared/components/Spinner';
import { currentMonth, formatMonth } from '../../shared/format';
import { useAuth } from '../auth/AuthContext';
import { PayslipView } from './PayslipView';
import { MONTH_PATTERN, usePayslip } from './usePayslip';

/** HR: `/employees/:id/payslip` for anyone. Employee: `/me/payslip` for their own record. */
export function PayslipPage() {
  const { id } = useParams();
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const monthInputId = useId();

  const employeeId = id ?? user?.employeeId ?? null;
  const requested = params.get('month') ?? '';
  const month = MONTH_PATTERN.test(requested) ? requested : currentMonth();
  const payslip = usePayslip(employeeId, month);

  const changeMonth = (value: string) => {
    // An emptied picker means "back to the default" rather than an invalid request.
    setParams(value ? { month: value } : {}, { replace: true });
  };

  return (
    <>
      <PageHeader
        title={id ? 'Payslip' : 'My payslip'}
        subtitle={id ? <Link to="/employees">← Back to employees</Link> : undefined}
      />

      <div className="card payslip-toolbar">
        <label htmlFor={monthInputId} className="field__label">
          Month
        </label>
        <input
          id={monthInputId}
          type="month"
          className="field__control payslip-toolbar__month"
          value={month}
          onChange={(event) => changeMonth(event.target.value)}
        />
      </div>

      {!employeeId && (
        <Alert tone="info" title="No employee record">
          <p>Your account isn't linked to an employee record, so there is no payslip to show.</p>
        </Alert>
      )}
      {payslip.loading && <Spinner label={`Loading payslip for ${formatMonth(month)}…`} />}
      {payslip.error !== undefined && <PayslipError error={payslip.error} />}
      {payslip.data && <PayslipView payslip={payslip.data} />}
    </>
  );
}

function PayslipError({ error }: { error: unknown }) {
  // 422 is an expected business outcome (inactive, not yet hired, deductions exceed gross), not a failure.
  if (isStatus(error, 422)) {
    return (
      <Alert tone="info" title="No payslip for this month">
        <p>{errorMessage(error)}</p>
      </Alert>
    );
  }
  if (isStatus(error, 404)) return <Alert title="Employee not found">It may have been deleted.</Alert>;
  if (isStatus(error, 403)) return <Alert title="Not allowed">You can only view your own payslip.</Alert>;
  return <Alert title="Couldn't load the payslip">{errorMessage(error)}</Alert>;
}
