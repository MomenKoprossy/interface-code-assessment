import { useState } from 'react';
import { deleteEmployee } from '../../api/employees';
import { errorMessage, isStatus } from '../../api/problem';
import type { EmployeeListItem } from '../../api/types';
import { Alert } from '../../shared/components/Alert';
import { ConfirmDialog } from '../../shared/components/ConfirmDialog';

export type DeleteOutcome = 'deleted' | 'already-deleted';

interface DeleteEmployeeDialogProps {
  employee: EmployeeListItem;
  onClose: () => void;
  onDone: (outcome: DeleteOutcome) => void;
}

export function DeleteEmployeeDialog({ employee, onClose, onDone }: DeleteEmployeeDialogProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    setBusy(true);
    setError(null);
    try {
      await deleteEmployee(employee.id);
      onDone('deleted');
    } catch (caught) {
      if (isStatus(caught, 404)) {
        onDone('already-deleted');
        return;
      }
      setError(errorMessage(caught));
      setBusy(false);
    }
  }

  return (
    <ConfirmDialog
      title="Delete employee?"
      confirmLabel="Delete employee"
      danger
      busy={busy}
      onConfirm={handleConfirm}
      onCancel={onClose}
    >
      <p>
        <strong>{employee.fullName}</strong> ({employee.email}) will be removed from the employee list. This can't be
        undone from the app.
      </p>
      {error && <Alert>{error}</Alert>}
    </ConfirmDialog>
  );
}
