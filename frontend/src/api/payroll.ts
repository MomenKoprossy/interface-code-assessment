import { request } from './http';
import type { Payslip } from './types';

export const getPayslip = (employeeId: string, month: string, signal?: AbortSignal) =>
  request<Payslip>(`/employees/${encodeURIComponent(employeeId)}/payslip`, { query: { month }, signal });

export const getPayrollPolicy = (signal?: AbortSignal) => request<{ currency: string }>('/payroll/policy', { signal });
