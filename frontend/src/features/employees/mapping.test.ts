import { describe, expect, it } from 'vitest';
import type { EmployeeDetails } from '../../api/types';
import { emptyFormValues, mapServerErrors, removeItemErrors, toEmployeeInput, toFormValues } from './mapping';

const employee: EmployeeDetails = {
  id: 'e1',
  fullName: 'Ada Lovelace',
  email: 'ada@example.com',
  jobTitle: 'Engineer',
  department: 'R&D',
  hireDate: '2020-01-15',
  baseMonthlySalary: 5000.5,
  status: 'Active',
  allowances: [{ name: 'Housing', amount: 250 }],
  deductions: [
    { name: 'Loan', amount: 100 },
    { name: 'Gym', amount: 30.25 },
  ],
};

describe('form <-> DTO mapping', () => {
  it('round-trips an employee through the form values', () => {
    const input = toEmployeeInput(toFormValues(employee));
    expect({ ...input, id: employee.id }).toEqual(employee);
  });

  it('trims text and converts amounts to numbers', () => {
    const values = {
      ...emptyFormValues(),
      fullName: '  Ada  ',
      email: ' ada@example.com ',
      baseMonthlySalary: ' 12.50 ',
      allowances: [{ key: 'k', name: ' Housing ', amount: '100.10' }],
    };
    const input = toEmployeeInput(values);
    expect(input.fullName).toBe('Ada');
    expect(input.email).toBe('ada@example.com');
    expect(input.baseMonthlySalary).toBe(12.5);
    expect(input.allowances).toEqual([{ name: 'Housing', amount: 100.1 }]);
  });

  it('gives every item row a unique key', () => {
    const values = toFormValues(employee);
    expect(new Set(values.deductions.map((d) => d.key)).size).toBe(2);
  });
});

describe('mapServerErrors', () => {
  const values = toFormValues(employee);

  it('maps top-level and nested item paths onto fields, first message wins', () => {
    const result = mapServerErrors(
      {
        email: ['Email is already used by another employee.', 'second'],
        'allowances[0].amount': ['Amount must be greater than 0.'],
        'deductions[1].name': ['Name must be unique.'],
      },
      values,
    );
    expect(result.fields).toEqual({
      email: 'Email is already used by another employee.',
      'allowances[0].amount': 'Amount must be greater than 0.',
      'deductions[1].name': 'Name must be unique.',
    });
    expect(result.unmapped).toEqual([]);
  });

  it('normalizes PascalCase paths', () => {
    const result = mapServerErrors({ 'Allowances[0].Name': ['Required.'], FullName: ['Too long.'] }, values);
    expect(result.fields).toEqual({ 'allowances[0].name': 'Required.', fullName: 'Too long.' });
  });

  it('keeps list-level errors on the list', () => {
    expect(mapServerErrors({ deductions: ['At most 20 items.'] }, values).fields).toEqual({
      deductions: 'At most 20 items.',
    });
  });

  it('sends unknown fields and out-of-range rows to the unmapped list', () => {
    const result = mapServerErrors(
      { managerId: ['Unknown manager.'], 'allowances[5].amount': ['Bad amount.'], '': ['General failure.'] },
      values,
    );
    expect(result.fields).toEqual({});
    expect(result.unmapped).toEqual(['Unknown manager.', 'Bad amount.', 'General failure.']);
  });
});

describe('removeItemErrors', () => {
  it('drops the removed row and shifts later rows of the same list up', () => {
    const errors = {
      fullName: 'Required.',
      'allowances[0].name': 'A0',
      'allowances[1].amount': 'A1',
      'allowances[2].name': 'A2',
      'deductions[2].name': 'D2',
    };
    expect(removeItemErrors(errors, 'allowances', 1)).toEqual({
      fullName: 'Required.',
      'allowances[0].name': 'A0',
      'allowances[1].name': 'A2',
      'deductions[2].name': 'D2',
    });
  });
});
