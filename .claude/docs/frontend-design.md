# Frontend design

React 19 + TypeScript (strict) + Vite + **plain SCSS** (no UI kit or CSS framework). The goal is a simple, clean, accessible UI.

## Dependencies (each justified, nothing else)

| Package | Why |
|---|---|
| `react`, `react-dom` | the framework the brief requires |
| `react-router-dom` | URL-driven pages (list, edit, payslip, login) with guarded routes; hand-rolling a router isn't a good use of time |
| `sass` (dev) | the brief asks for SCSS: partials, variables, mixins |
| `vite`, `@vitejs/plugin-react`, `typescript` (dev) | build tooling |
| `vitest`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `jsdom` (dev) | component and unit tests |
| `eslint` + `typescript-eslint` + `eslint-plugin-react-hooks` (dev) | static checks in CI |
| `@types/node` (dev) | types for `process.env` in `vite.config.ts` (build-time only) |
| `@testing-library/dom` (dev) | required peer dependency of `@testing-library/react` 16 |

`package.json` has two supply-chain settings and no extra packages:
- **`overrides.minimatch: ^11.0.6`**: ESLint and typescript-eslint still ask for `minimatch` 10, which has a moderate ReDoS
  advisory (GHSA-g3hv-75wg-m249, fixed in 11.0.1). It's dev-only and lint passes on 11. Remove the override once
  upstream moves.
- **`allowScripts: { "@parcel/watcher": false }`**: sass's optional file watcher ships an install script. We never use
  `sass --watch` (Vite compiles the SCSS), so its script is denied explicitly instead of left unreviewed.

**Not used, on purpose:** no UI kit, no state library (React context + hooks are enough), no form library
(the forms are small, and the validation rules live in our own tested module), no data-fetching library (a small
typed client + hooks), no date library (`<input type="date">` / `type="month"` and `Intl`).

## Structure

```
frontend/src/
  main.tsx, App.tsx            routes + providers
  api/
    http.ts                    fetch wrapper: base URL, Bearer token, JSON, ProblemDetails → ApiError
    problem.ts                 ApiError type (status, title, detail, fieldErrors, traceId) + helpers
    types.ts                   DTO types copied from .claude/docs/api-contract.md
    employees.ts, payroll.ts, auth.ts   typed endpoint functions
  features/
    auth/      AuthContext (token in sessionStorage), LoginPage, RequireAuth / RequireRole
    employees/ EmployeesListPage, EmployeeFormPage (create+edit), DeleteEmployeeDialog,
               useEmployees (list/query state), validation.ts (rules mirroring the server), mapping.ts
    payroll/   PayslipPage (HR: any employee; Employee: own), PayslipView, usePayslip
  shared/
    components/  Button, TextField, SelectField, MoneyField, Pagination, Alert, ConfirmDialog, Spinner, PageHeader
    format.ts    formatMoney (Intl.NumberFormat with currency), formatDate, formatMonth
  styles/
    _tokens.scss (colors, spacing, radius, font), _mixins.scss, _base.scss, _layout.scss,
    components/*.scss, main.scss
```

## Behaviour

- **Routing:** `/login`; HR → `/employees`, `/employees/new`, `/employees/:id/edit`, `/employees/:id/payslip`;
  Employee → `/me/payslip`. Unknown routes go to the user's home page. Role guards are UX only: the server enforces
  everything.
- **List:** search (debounced 300 ms), department select (from `/api/employees/departments`), status select,
  pagination (prev/next + "page X of Y", page size 10). Query state lives in the URL search params, so it's
  shareable and back-button friendly. Empty and loading states are handled. Salaries show in the policy currency
  (`GET /api/payroll/policy`). The result count is a live `status`.
- **Form:** one component for create and edit. Allowance/deduction rows can be added and removed. Client-side validation
  runs on blur and on submit using `validation.ts`. Server 400/409 `errors` are mapped onto the same fields (camelCase
  paths), and anything unmapped goes to a top-of-form `Alert`. The submit button is disabled while saving, and after a
  failed submit focus moves to the first invalid field (the login page does the same). Money inputs show the currency
  code as a decorative suffix. Values stay plain decimals.
- **Delete:** a confirm dialog names the employee. On success the list refreshes; a 404 shows "already deleted".
- **Payslip:** a `type="month"` picker (defaults to the current month) and a breakdown table (earnings, SI with cap
  note, tax per bracket, other deductions, net). A 422 shows the server's `detail` as a friendly notice, not an error.
  A malformed `?month=` falls back to the current month, and the URL is corrected to match.
- **Errors:** every API failure becomes an `ApiError` with `status`, `title`, `detail`, `fieldErrors`. A 401
  clears the session and redirects to login. Network failures show "Can't reach the server". After a deliberate sign-out
  the login page forgets the last page, so the next user lands on their own home. A deep link or an expired session still
  returns to the page.

## Visual design ("simple design")

Neutral light theme with one accent color (indigo). System font stack, 8 px spacing scale, cards with a subtle
border, a table with zebra rows, clear focus rings, color plus icon/text for status (never color alone). It's
responsive down to 360 px: the table collapses into cards on narrow screens.
Dark mode is out of scope.

## Accessibility basics

Labels bound to inputs. Field errors are linked with `aria-describedby` and announced through `role="alert"`. Dialogs trap
focus, close on Escape and restore focus. All controls are reachable by keyboard. Contrast ≥ 4.5:1.
