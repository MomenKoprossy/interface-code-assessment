import { getPayslip } from '../../api/payroll';
import { useAsync } from '../../shared/useAsync';

export const MONTH_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

export function usePayslip(employeeId: string | null, month: string) {
  const key = employeeId && MONTH_PATTERN.test(month) ? `${employeeId}|${month}` : null;
  return useAsync(key, (signal) => getPayslip(employeeId ?? '', month, signal));
}
