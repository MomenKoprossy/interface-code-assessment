import type { EmployeeStatus } from '../../api/types';
import { formatDate } from '../../shared/format';

// Mirrors the server's validation table in api-contract.md. The server stays the authority; this only gives
// faster feedback. Error keys use the server's camelCase property paths so server and client errors share fields.

export interface PayItemDraft {
  /** Stable React key; never sent to the server. */
  key: string;
  name: string;
  amount: string;
}

export type PayItemList = 'allowances' | 'deductions';

export interface EmployeeFormValues {
  fullName: string;
  email: string;
  jobTitle: string;
  department: string;
  hireDate: string;
  baseMonthlySalary: string;
  status: EmployeeStatus;
  allowances: PayItemDraft[];
  deductions: PayItemDraft[];
}

export type FormErrors = Record<string, string>;

export const LIMITS = {
  textMax: 100,
  emailMax: 254,
  itemNameMax: 50,
  itemsMax: 20,
  moneyMax: 1_000_000,
  earliestHireDate: '1900-01-01',
} as const;

const EMAIL_PATTERN = /^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$/;
const MONEY_PATTERN = /^-?\d+(\.\d+)?$/;
const ISO_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

function requiredText(value: string, label: string, max: number): string | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return `${label} is required.`;
  if (trimmed.length > max) return `${label} must be at most ${max} characters.`;
  return undefined;
}

export function validateEmail(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return 'Email is required.';
  if (trimmed.length > LIMITS.emailMax) return `Email must be at most ${LIMITS.emailMax} characters.`;
  if (!EMAIL_PATTERN.test(trimmed)) return 'Enter a valid email address, like name@example.com.';
  return undefined;
}

/** `today` is `YYYY-MM-DD` in UTC (ISO strings compare correctly as text). */
export function validateHireDate(value: string, today: string): string | undefined {
  if (value === '') return 'Hire date is required.';
  if (!ISO_DATE_PATTERN.test(value) || Number.isNaN(Date.parse(value))) return 'Enter a valid date.';
  if (value > today) return 'Hire date cannot be in the future.';
  if (value < LIMITS.earliestHireDate) return `Hire date cannot be before ${formatDate(LIMITS.earliestHireDate)}.`;
  return undefined;
}

export function validateMoney(value: string, label: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return `${label} is required.`;
  if (!MONEY_PATTERN.test(trimmed)) return `${label} must be a number.`;
  if ((trimmed.split('.')[1] ?? '').replace(/0+$/, '').length > 2) return `${label} can have at most 2 decimals.`;
  const amount = Number(trimmed);
  if (amount <= 0) return `${label} must be greater than 0.`;
  if (amount > LIMITS.moneyMax) return `${label} must be at most 1,000,000.`;
  return undefined;
}

export function validateStatus(value: string): string | undefined {
  return value === 'Active' || value === 'Inactive' ? undefined : 'Status must be Active or Inactive.';
}

function validateItems(list: PayItemList, items: PayItemDraft[]): FormErrors {
  const errors: FormErrors = {};
  if (items.length > LIMITS.itemsMax) errors[list] = `At most ${LIMITS.itemsMax} items are allowed.`;
  // Every row of a duplicate pair is flagged, so the row the user just edited shows the error too.
  const counts = new Map<string, number>();
  for (const item of items) {
    const key = item.name.trim().toLowerCase();
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  items.forEach((item, index) => {
    const nameError = requiredText(item.name, 'Name', LIMITS.itemNameMax);
    if (nameError) errors[`${list}[${index}].name`] = nameError;
    else if ((counts.get(item.name.trim().toLowerCase()) ?? 0) > 1) {
      errors[`${list}[${index}].name`] = 'Name must be unique in this list.';
    }

    const amountError = validateMoney(item.amount, 'Amount');
    if (amountError) errors[`${list}[${index}].amount`] = amountError;
  });
  return errors;
}

export function validateEmployee(values: EmployeeFormValues, today: string): FormErrors {
  const candidates: Record<string, string | undefined> = {
    fullName: requiredText(values.fullName, 'Full name', LIMITS.textMax),
    email: validateEmail(values.email),
    jobTitle: requiredText(values.jobTitle, 'Job title', LIMITS.textMax),
    department: requiredText(values.department, 'Department', LIMITS.textMax),
    hireDate: validateHireDate(values.hireDate, today),
    baseMonthlySalary: validateMoney(values.baseMonthlySalary, 'Base monthly salary'),
    status: validateStatus(values.status),
  };
  const errors: FormErrors = {};
  for (const [field, message] of Object.entries(candidates)) {
    if (message) errors[field] = message;
  }
  return {
    ...errors,
    ...validateItems('allowances', values.allowances),
    ...validateItems('deductions', values.deductions),
  };
}

/**
 * The error for one field path (e.g. `allowances[2].amount`), used on blur.
 * Runs the whole-form rules so cross-item rules such as unique names stay consistent.
 */
export function validateField(path: string, values: EmployeeFormValues, today: string): string | undefined {
  return validateEmployee(values, today)[path];
}
