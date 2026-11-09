# API contract (v1)

> Frozen before the parallel build so backend and frontend agents can work at the same time.
> Base path: `/api`. JSON uses camelCase. Enums are strings. Dates are `YYYY-MM-DD` (`DateOnly`) and months are
> `YYYY-MM`. Money is a JSON number with at most 2 decimals; the server owns all money arithmetic and the
> UI only formats it.

## Errors: RFC 7807 ProblemDetails (every non-2xx)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "optional human message",
  "traceId": "00-…",
  "errors": { "email": ["Email is already used by another employee."], "allowances[0].amount": ["Amount must be greater than 0."] }
}
```

- `errors` keys are **camelCase property paths** (`fullName`, `allowances[1].name`). The UI maps them onto fields.
- 400 validation · 401 unauthenticated · 403 forbidden · 404 not found · 409 conflict (`errors.email` on a
  duplicate email) · 422 business rule (payslip not computable) · 429 rate limited · 500 unexpected (no internals leaked).

## Auth

| Method | Path | Auth | Body → Response |
|---|---|---|---|
| POST | `/api/auth/login` | anonymous | `{ email, password }` → 200 `LoginResponse`; 400 malformed input (email format, max 254; password required, max 128); 401 ProblemDetails on bad credentials (same message for unknown email and wrong password) |
| GET | `/api/auth/me` | any authenticated | → 200 `CurrentUser`; 401 if the token's user no longer exists |

```ts
type Role = 'HR' | 'Employee';
interface CurrentUser { id: string; email: string; displayName: string; role: Role; employeeId: string | null; permissions: string[] }
interface LoginResponse { accessToken: string; expiresAtUtc: string; user: CurrentUser }
```

Permissions: `employees.read`, `employees.write`, `payroll.read.all` (HR); `payroll.read.own` (Employee).
JWT claims: `sub` (user id), `name`, `email`, `role`, `employee_id` (Employee role only), repeated `permission`.

## Employees (HR)

| Method | Path | Permission | Response |
|---|---|---|---|
| GET | `/api/employees?page=1&pageSize=10&search=&department=&status=` | employees.read | 200 `PagedResult<EmployeeListItem>` |
| GET | `/api/employees/departments` | employees.read | 200 `string[]` (distinct, sorted) |
| GET | `/api/employees/{id}` | employees.read | 200 `EmployeeDetails` · 404 |
| POST | `/api/employees` | employees.write | `EmployeeInput` → 201 `EmployeeDetails` + `Location` · 400 · 409 |
| PUT | `/api/employees/{id}` | employees.write | `EmployeeInput` → 200 `EmployeeDetails` · 400 · 404 · 409 |
| DELETE | `/api/employees/{id}` | employees.write | 204 · 404 (soft delete) |

List semantics: `search` is a case-insensitive substring match on full name **or** email. `department` is an exact,
case-insensitive match. `status` is `Active` or `Inactive` (case-sensitive; any other value → 400 `errors.status`). Results are ordered by full name, then id. `pageSize` is
clamped to 1..100 (default 10) and `page` is at least 1.

```ts
type EmployeeStatus = 'Active' | 'Inactive';
interface PayItem { name: string; amount: number }
interface EmployeeInput {
  fullName: string; email: string; jobTitle: string; department: string;
  hireDate: string;            // YYYY-MM-DD
  baseMonthlySalary: number; status: EmployeeStatus;
  allowances: PayItem[];       // may be empty
  deductions: PayItem[];       // "other deductions", may be empty
}
interface EmployeeListItem { id: string; fullName: string; email: string; jobTitle: string; department: string; hireDate: string; baseMonthlySalary: number; status: EmployeeStatus }
interface EmployeeDetails extends EmployeeListItem { allowances: PayItem[]; deductions: PayItem[] }
interface PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasPrevious: boolean; hasNext: boolean }
```

### Validation rules (server is the authority; UI mirrors them)

| Field | Rule |
|---|---|
| fullName | required (not blank after trim), max 100 |
| email | required, valid format (`local@domain.tld`, no spaces), max 254, unique (case-insensitive, among non-deleted employees) → 409 |
| jobTitle | required, max 100 |
| department | required, max 100 |
| hireDate | required, not in the future (compared to **today, UTC**), not before 1900-01-01 |
| baseMonthlySalary | required, > 0, ≤ 1,000,000, at most 2 decimals |
| status | `Active` or `Inactive` |
| allowances / deductions | at most 20 items each; item name required, max 50, unique within its list (case-insensitive); item amount > 0, ≤ 1,000,000, at most 2 decimals |

Text fields are trimmed and email is lower-cased before storing.

## Payroll

| Method | Path | Auth | Response |
|---|---|---|---|
| GET | `/api/employees/{id}/payslip?month=YYYY-MM` | `payroll.read.all`, or `payroll.read.own` when `{id}` is the caller's `employee_id` | 200 `Payslip` · 400 bad/missing month · 403 another employee's payslip · 404 employee · 422 inactive employee / month before hire month / deductions exceed gross |
| GET | `/api/payroll/policy` | any authenticated | 200 `PayrollPolicy` (read-only view of the configured rules) |

```ts
interface Payslip {
  employeeId: string; employeeName: string; department: string; jobTitle: string;
  month: string;                         // YYYY-MM
  currency: string;                      // ISO code from config, display only
  baseSalary: number;
  allowances: PayItem[]; totalAllowances: number;
  grossPay: number;                      // baseSalary + totalAllowances
  socialInsurance: { rate: number; monthlyCap: number | null; uncappedAmount: number; amount: number; capApplied: boolean };
  taxableIncome: number;                 // grossPay − socialInsurance.amount (if policy deducts SI before tax) else grossPay
  incomeTax: { amount: number; brackets: TaxBracketLine[] };
  otherDeductions: PayItem[]; totalOtherDeductions: number;
  totalDeductions: number;               // socialInsurance.amount + incomeTax.amount + totalOtherDeductions
  netPay: number;                        // grossPay − totalDeductions
  roundingRule: string;                  // human-readable description of the rule applied
}
interface TaxBracketLine { from: number; upTo: number | null; rate: number; taxedAmount: number; tax: number }
interface PayrollPolicy {
  currency: string; deductSocialInsuranceBeforeTax: boolean;
  socialInsurance: { rate: number; monthlyCap: number | null };
  taxBrackets: { upTo: number | null; rate: number }[];
  roundingRule: string;
}
```

## Ops

`GET /health` (readiness, checks the DB) · `GET /alive` (liveness) · `GET /openapi/v1.json` · `GET /swagger`
