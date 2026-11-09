// DTOs copied from .claude/docs/api-contract.md (v1). Keep in sync with the contract, not with server internals.

export type Role = 'HR' | 'Employee';

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  employeeId: string | null;
  permissions: string[];
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: CurrentUser;
}

export type EmployeeStatus = 'Active' | 'Inactive';

export interface PayItem {
  name: string;
  amount: number;
}

export interface EmployeeInput {
  fullName: string;
  email: string;
  jobTitle: string;
  department: string;
  hireDate: string;
  baseMonthlySalary: number;
  status: EmployeeStatus;
  allowances: PayItem[];
  deductions: PayItem[];
}

export interface EmployeeListItem {
  id: string;
  fullName: string;
  email: string;
  jobTitle: string;
  department: string;
  hireDate: string;
  baseMonthlySalary: number;
  status: EmployeeStatus;
}

export interface EmployeeDetails extends EmployeeListItem {
  allowances: PayItem[];
  deductions: PayItem[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface TaxBracketLine {
  from: number;
  upTo: number | null;
  rate: number;
  taxedAmount: number;
  tax: number;
}

export interface Payslip {
  employeeId: string;
  employeeName: string;
  department: string;
  jobTitle: string;
  month: string;
  currency: string;
  baseSalary: number;
  allowances: PayItem[];
  totalAllowances: number;
  grossPay: number;
  socialInsurance: {
    rate: number;
    monthlyCap: number | null;
    uncappedAmount: number;
    amount: number;
    capApplied: boolean;
  };
  taxableIncome: number;
  incomeTax: { amount: number; brackets: TaxBracketLine[] };
  otherDeductions: PayItem[];
  totalOtherDeductions: number;
  totalDeductions: number;
  netPay: number;
  roundingRule: string;
}
