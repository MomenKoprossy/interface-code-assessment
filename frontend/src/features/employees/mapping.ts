import type { FieldErrors } from '../../api/problem';
import type { EmployeeDetails, EmployeeInput, PayItem } from '../../api/types';
import type { EmployeeFormValues, FormErrors, PayItemDraft, PayItemList } from './validation';

let nextKey = 0;
export function newPayItemDraft(item?: PayItem): PayItemDraft {
  nextKey += 1;
  return { key: `item-${nextKey}`, name: item?.name ?? '', amount: item ? item.amount.toFixed(2) : '' };
}

export function emptyFormValues(): EmployeeFormValues {
  return {
    fullName: '',
    email: '',
    jobTitle: '',
    department: '',
    hireDate: '',
    baseMonthlySalary: '',
    status: 'Active',
    allowances: [],
    deductions: [],
  };
}

export function toFormValues(employee: EmployeeDetails): EmployeeFormValues {
  return {
    fullName: employee.fullName,
    email: employee.email,
    jobTitle: employee.jobTitle,
    department: employee.department,
    hireDate: employee.hireDate,
    baseMonthlySalary: employee.baseMonthlySalary.toFixed(2),
    status: employee.status,
    allowances: employee.allowances.map(newPayItemDraft),
    deductions: employee.deductions.map(newPayItemDraft),
  };
}

const toPayItem = (draft: PayItemDraft): PayItem => ({ name: draft.name.trim(), amount: Number(draft.amount.trim()) });

export function toEmployeeInput(values: EmployeeFormValues): EmployeeInput {
  return {
    fullName: values.fullName.trim(),
    email: values.email.trim(),
    jobTitle: values.jobTitle.trim(),
    department: values.department.trim(),
    hireDate: values.hireDate,
    baseMonthlySalary: Number(values.baseMonthlySalary.trim()),
    status: values.status,
    allowances: values.allowances.map(toPayItem),
    deductions: values.deductions.map(toPayItem),
  };
}

const SIMPLE_FIELDS = new Set(['fullName', 'email', 'jobTitle', 'department', 'hireDate', 'baseMonthlySalary', 'status']);
const ITEM_PATH = /^(allowances|deductions)\[(\d+)\]\.(name|amount)$/;

/** `Allowances[0].Amount` → `allowances[0].amount`: tolerate PascalCase paths even though the contract says camelCase. */
function normalizePath(path: string): string {
  return path
    .split('.')
    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
    .join('.');
}

function isFieldOnForm(path: string, values: EmployeeFormValues): boolean {
  if (SIMPLE_FIELDS.has(path) || path === 'allowances' || path === 'deductions') return true;
  const match = ITEM_PATH.exec(path);
  if (!match) return false;
  const list = match[1] as PayItemList;
  return Number(match[2]) < values[list].length;
}

export interface MappedServerErrors {
  fields: FormErrors;
  /** Messages with no matching field on the form; shown in the top-of-form alert. */
  unmapped: string[];
}

/** Splits ProblemDetails `errors` into per-field messages (first message wins) and the rest. */
export function mapServerErrors(errors: FieldErrors, values: EmployeeFormValues): MappedServerErrors {
  const fields: FormErrors = {};
  const unmapped: string[] = [];
  for (const [rawPath, messages] of Object.entries(errors)) {
    const path = normalizePath(rawPath);
    if (isFieldOnForm(path, values) && messages[0]) fields[path] = messages[0];
    else unmapped.push(...messages);
  }
  return { fields, unmapped };
}

/** After removing row `index`, error keys for later rows move up one so they stay on the same inputs. */
export function removeItemErrors(errors: FormErrors, list: PayItemList, index: number): FormErrors {
  const next: FormErrors = {};
  for (const [path, message] of Object.entries(errors)) {
    const match = ITEM_PATH.exec(path);
    if (!match || match[1] !== list) {
      next[path] = message;
      continue;
    }
    const row = Number(match[2]);
    if (row < index) next[path] = message;
    else if (row > index) next[`${list}[${row - 1}].${match[3]}`] = message;
  }
  return next;
}
