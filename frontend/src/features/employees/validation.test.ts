import { describe, expect, it } from 'vitest';
import { newPayItemDraft } from './mapping';
import {
  validateEmail,
  validateEmployee,
  validateField,
  validateHireDate,
  validateMoney,
  type EmployeeFormValues,
} from './validation';

const TODAY = '2026-11-09';

function validValues(overrides: Partial<EmployeeFormValues> = {}): EmployeeFormValues {
  return {
    fullName: 'Ada Lovelace',
    email: 'ada@example.com',
    jobTitle: 'Engineer',
    department: 'R&D',
    hireDate: '2020-01-15',
    baseMonthlySalary: '5000',
    status: 'Active',
    allowances: [],
    deductions: [],
    ...overrides,
  };
}

const items = (...pairs: [string, string][]) => pairs.map(([name, amount]) => ({ ...newPayItemDraft(), name, amount }));

describe('validateEmployee', () => {
  it('accepts a valid employee', () => {
    expect(validateEmployee(validValues(), TODAY)).toEqual({});
  });

  it.each([
    ['fullName', '', 'Full name is required.'],
    ['fullName', '   ', 'Full name is required.'],
    ['jobTitle', '', 'Job title is required.'],
    ['department', ' ', 'Department is required.'],
  ] as const)('requires %s (value %j)', (field, value, message) => {
    expect(validateEmployee(validValues({ [field]: value }), TODAY)[field]).toBe(message);
  });

  it.each([
    ['fullName', 100, undefined],
    ['fullName', 101, 'Full name must be at most 100 characters.'],
    ['jobTitle', 101, 'Job title must be at most 100 characters.'],
    ['department', 101, 'Department must be at most 100 characters.'],
  ] as const)('limits %s to 100 characters (length %i)', (field, length, message) => {
    expect(validateEmployee(validValues({ [field]: 'a'.repeat(length) }), TODAY)[field]).toBe(message);
  });

  it('measures length after trimming, like the server stores it', () => {
    expect(validateEmployee(validValues({ fullName: ` ${'a'.repeat(100)} ` }), TODAY).fullName).toBeUndefined();
  });

  it('rejects an unknown status', () => {
    const values = { ...validValues(), status: 'Retired' } as unknown as EmployeeFormValues;
    expect(validateEmployee(values, TODAY).status).toBe('Status must be Active or Inactive.');
  });
});

describe('validateEmail', () => {
  it.each([
    ['', 'Email is required.'],
    ['ada', 'Enter a valid email address, like name@example.com.'],
    ['ada@example', 'Enter a valid email address, like name@example.com.'],
    ['ada lovelace@example.com', 'Enter a valid email address, like name@example.com.'],
    ['ada@example.com', undefined],
    ['  ada@example.com  ', undefined],
  ])('%j → %j', (value, expected) => {
    expect(validateEmail(value)).toBe(expected);
  });

  it('allows 254 characters and rejects 255', () => {
    const domain = '@example.com';
    expect(validateEmail('a'.repeat(254 - domain.length) + domain)).toBeUndefined();
    expect(validateEmail('a'.repeat(255 - domain.length) + domain)).toBe('Email must be at most 254 characters.');
  });
});

describe('validateHireDate', () => {
  it.each([
    ['today is allowed', TODAY, undefined],
    ['tomorrow is in the future', '2026-11-10', 'Hire date cannot be in the future.'],
    ['1900-01-01 is the earliest allowed', '1900-01-01', undefined],
    ['1899-12-31 is too early', '1899-12-31', 'Hire date cannot be before 1900-01-01.'],
    ['empty is required', '', 'Hire date is required.'],
    ['garbage is invalid', '15/01/2020', 'Enter a valid date.'],
  ])('%s', (_, value, expected) => {
    expect(validateHireDate(value, TODAY)).toBe(expected);
  });
});

describe('validateMoney', () => {
  it.each([
    ['0', 'Salary must be greater than 0.'],
    ['0.00', 'Salary must be greater than 0.'],
    ['0.01', undefined],
    ['1000000', undefined],
    ['1000000.00', undefined],
    ['1000000.01', 'Salary must be at most 1,000,000.'],
    ['12.34', undefined],
    ['12.345', 'Salary can have at most 2 decimals.'],
    ['-5', 'Salary must be a number.'],
    ['1e3', 'Salary must be a number.'],
    ['abc', 'Salary must be a number.'],
    ['', 'Salary is required.'],
  ])('%j → %j', (value, expected) => {
    expect(validateMoney(value, 'Salary')).toBe(expected);
  });
});

describe('allowances and deductions', () => {
  it('accepts an empty list', () => {
    expect(validateEmployee(validValues({ allowances: [], deductions: [] }), TODAY)).toEqual({});
  });

  it('reports item errors with the server path format', () => {
    const errors = validateEmployee(validValues({ allowances: items(['Housing', '100'], ['', '0']) }), TODAY);
    expect(errors).toEqual({
      'allowances[1].name': 'Name is required.',
      'allowances[1].amount': 'Amount must be greater than 0.',
    });
  });

  it('limits item names to 50 characters', () => {
    const ok = validateEmployee(validValues({ deductions: items(['a'.repeat(50), '1']) }), TODAY);
    const tooLong = validateEmployee(validValues({ deductions: items(['a'.repeat(51), '1']) }), TODAY);
    expect(ok).toEqual({});
    expect(tooLong['deductions[0].name']).toBe('Name must be at most 50 characters.');
  });

  it('flags a case-insensitive duplicate name on the later row only', () => {
    const errors = validateEmployee(validValues({ allowances: items(['Housing', '1'], [' housing ', '2']) }), TODAY);
    expect(errors).toEqual({ 'allowances[1].name': 'Name must be unique in this list.' });
  });

  it('allows the same name in allowances and deductions', () => {
    const values = validValues({ allowances: items(['Transport', '1']), deductions: items(['Transport', '1']) });
    expect(validateEmployee(values, TODAY)).toEqual({});
  });

  it('allows 20 items and rejects 21', () => {
    const make = (n: number) => items(...Array.from({ length: n }, (_, i): [string, string] => [`Item ${i}`, '1']));
    expect(validateEmployee(validValues({ allowances: make(20) }), TODAY)).toEqual({});
    expect(validateEmployee(validValues({ allowances: make(21) }), TODAY).allowances).toBe('At most 20 items are allowed.');
  });
});

describe('validateField', () => {
  it('returns only the requested field error', () => {
    const values = validValues({ fullName: '', email: 'bad' });
    expect(validateField('email', values, TODAY)).toBe('Enter a valid email address, like name@example.com.');
    expect(validateField('jobTitle', values, TODAY)).toBeUndefined();
  });
});
