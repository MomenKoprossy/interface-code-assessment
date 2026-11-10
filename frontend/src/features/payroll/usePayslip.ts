import { getPayslip } from '../../api/payroll';
import { useAsync } from '../../shared/useAsync';

// Years 1900-9999, the range the API accepts.
export const MONTH_PATTERN = /^(19\d{2}|[2-9]\d{3})-(0[1-9]|1[0-2])$/;

export function usePayslip(employeeId: string | null, month: string) {
  const key = employeeId && MONTH_PATTERN.test(month) ? `${employeeId}|${month}` : null;
  return useAsync(key, (signal) => getPayslip(employeeId ?? '', month, signal));
}
