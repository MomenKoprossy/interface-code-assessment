# Testing strategy

Tests ship with the feature (platform rule: "tests are mandatory at every layer").

| Suite | Location | Needs Docker | What it proves |
|---|---|---|---|
| Payroll unit | `backend/tests/Payroll.UnitTests` | no | Calculator + every rule + edge case in `payroll-rules.md` (boundaries, cap, rounding midpoint, inactive, before hire, negative net, empty lists, policy validation, own-payslip access rule) |
| Employees unit | `backend/tests/Employees.UnitTests` | no | Every validation rule in `api-contract.md` (boundaries: 100/101 chars, salary 0/0.01, 2 vs 3 decimals, hire date today vs tomorrow with a pinned `IClock`), domain behaviour (normalization, deactivate event), handlers with NSubstitute ports (409 on duplicate email, 404s) |
| Identity unit | `backend/tests/Identity.UnitTests` | no | Token claims per role, login with wrong password → 401 with the generic message, password hashing round-trip |
| Architecture | `backend/tests/Architecture.Tests` | no | Layer rules from `architecture.md` §2 (module cores free of EF/ASP.NET, Payroll → Employees only through Contracts, BuildingBlocks never reference modules) |
| API integration | `backend/tests/Api.IntegrationTests` | **yes** (Testcontainers Postgres) | Real HTTP through the whole pipeline: CRUD, ProblemDetails shape, 400/401/403/404/409/422 mapping, pagination + filters, Employee role sees only their own payslip, soft-deleted email reusable |
| BuildingBlocks unit | `backend/src/BuildingBlocks/tests/BuildingBlocks.UnitTests` | no | Vendored platform behaviour (mediator ordering, Result mapping, outbox) |
| Frontend unit/component | `frontend/src/**/*.test.ts(x)` (Vitest + RTL) | no | validation rules, ProblemDetails parsing → field errors, money formatting, form shows server errors, list pagination controls, payslip rendering + 422 notice |

Rules:
- Unit tests are isolated: no database, no network, no real clock (`IClock` is substituted).
- Use **boundary values** and named test cases (`[Theory]` with inline data), not only the happy path.
- Assertions use AwesomeAssertions. Mocks use NSubstitute (already in the template's CPM).
- `dotnet test` on MTP: never pass `--nologo`.

Commands:
```bash
dotnet test backend/EmployeePayroll.slnx                     # all backend suites (integration needs Docker)
dotnet test backend/src/BuildingBlocks/BuildingBlocks.slnx   # vendored platform blocks
cd frontend && npm test                                      # vitest run
```
