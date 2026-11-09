import { listDepartments, listEmployees, type EmployeeQuery } from '../../api/employees';
import { useAsync } from '../../shared/useAsync';

export const PAGE_SIZE = 10;

/** The list's query lives in the URL (shareable, back-button friendly); this reads it defensively. */
export function parseListQuery(params: URLSearchParams): EmployeeQuery {
  const page = Number.parseInt(params.get('page') ?? '', 10);
  const status = params.get('status');
  return {
    page: Number.isFinite(page) && page > 0 ? page : 1,
    pageSize: PAGE_SIZE,
    search: params.get('search')?.trim() || undefined,
    department: params.get('department') || undefined,
    status: status === 'Active' || status === 'Inactive' ? status : undefined,
  };
}

/** Applies changes to the URL params; any filter change resets to page 1, and defaults are omitted. */
export function updateListParams(
  params: URLSearchParams,
  changes: Partial<Record<'page' | 'search' | 'department' | 'status', string>>,
): URLSearchParams {
  const next = new URLSearchParams(params);
  for (const [name, value] of Object.entries(changes)) {
    if (value) next.set(name, value);
    else next.delete(name);
  }
  if (!('page' in changes)) next.delete('page');
  if (next.get('page') === '1') next.delete('page');
  return next;
}

export function useEmployees(query: EmployeeQuery) {
  return useAsync(JSON.stringify(query), (signal) => listEmployees(query, signal));
}

export function useDepartments() {
  return useAsync('departments', listDepartments);
}
