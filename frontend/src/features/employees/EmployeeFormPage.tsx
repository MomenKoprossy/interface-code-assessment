import { Link, useNavigate, useParams } from 'react-router-dom';
import { createEmployee, getEmployee, updateEmployee } from '../../api/employees';
import { errorMessage, isStatus } from '../../api/problem';
import type { EmployeeInput } from '../../api/types';
import { Alert } from '../../shared/components/Alert';
import { PageHeader } from '../../shared/components/PageHeader';
import { Spinner } from '../../shared/components/Spinner';
import { useAsync } from '../../shared/useAsync';
import { EmployeeForm } from './EmployeeForm';
import { emptyFormValues, toFormValues } from './mapping';

/** One page for create (`/employees/new`) and edit (`/employees/:id/edit`). */
export function EmployeeFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const existing = useAsync(id ?? null, (signal) => getEmployee(id ?? '', signal));

  async function save(input: EmployeeInput) {
    const saved = id ? await updateEmployee(id, input) : await createEmployee(input);
    navigate('/employees', { state: { notice: `${saved.fullName} was ${id ? 'updated' : 'added'}.` } });
  }

  const backLink = <Link to="/employees">← Back to employees</Link>;

  if (!id) {
    return (
      <>
        <PageHeader title="Add employee" subtitle={backLink} />
        <EmployeeForm initialValues={emptyFormValues()} submitLabel="Add employee" onSubmit={save} />
      </>
    );
  }

  return (
    <>
      <PageHeader title={existing.data ? `Edit ${existing.data.fullName}` : 'Edit employee'} subtitle={backLink} />
      {existing.loading && <Spinner label="Loading employee…" />}
      {existing.error !== undefined && (
        <Alert title={isStatus(existing.error, 404) ? 'Employee not found' : "Couldn't load the employee"}>
          <p>{isStatus(existing.error, 404) ? 'It may have been deleted.' : errorMessage(existing.error)}</p>
        </Alert>
      )}
      {existing.data && (
        <EmployeeForm key={existing.data.id} initialValues={toFormValues(existing.data)} submitLabel="Save changes" onSubmit={save} />
      )}
    </>
  );
}
