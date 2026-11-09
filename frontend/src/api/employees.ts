import { request } from './http';
import type { EmployeeDetails, EmployeeInput, EmployeeListItem, EmployeeStatus, PagedResult } from './types';

export interface EmployeeQuery {
  page: number;
  pageSize: number;
  search?: string;
  department?: string;
  status?: EmployeeStatus;
}

export const listEmployees = (query: EmployeeQuery, signal?: AbortSignal) =>
  request<PagedResult<EmployeeListItem>>('/employees', { query: { ...query }, signal });

export const listDepartments = (signal?: AbortSignal) => request<string[]>('/employees/departments', { signal });

export const getEmployee = (id: string, signal?: AbortSignal) =>
  request<EmployeeDetails>(`/employees/${encodeURIComponent(id)}`, { signal });

export const createEmployee = (input: EmployeeInput) =>
  request<EmployeeDetails>('/employees', { method: 'POST', body: input });

export const updateEmployee = (id: string, input: EmployeeInput) =>
  request<EmployeeDetails>(`/employees/${encodeURIComponent(id)}`, { method: 'PUT', body: input });

export const deleteEmployee = (id: string) =>
  request<void>(`/employees/${encodeURIComponent(id)}`, { method: 'DELETE' });
