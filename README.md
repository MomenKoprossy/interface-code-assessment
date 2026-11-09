# Employee Management & Payroll

A small HR application: employee CRUD with validation, search and pagination, plus a configurable monthly payroll
calculator (progressive tax brackets, capped social insurance). It has an ASP.NET Core (.NET 10) API, a React +
TypeScript frontend with hand-written SCSS, and PostgreSQL.

It's built on my own **DotnetPlayGround** platform template, cut down to what this problem needs: a modular monolith on
the template's building blocks, with service-grade module boundaries enforced by architecture tests.

---

## Run it

Prerequisite: Docker.

```bash
cp .env.example .env          # then set JWT_KEY, POSTGRES_PASSWORD and DEMO_PASSWORD
docker compose up --build
```

Open **http://localhost:8080**. Swagger UI is at http://localhost:8080/swagger.

| Demo login | Role | Can do |
|---|---|---|
| `hr@acme.test` | HR | everything: employees CRUD, any payslip |
| `sara.ahmed@acme.test`, `omar.hassan@acme.test` | Employee | only their own payslip |

Password for all three: the `DEMO_PASSWORD` you put in `.env`. The database is seeded with about 15 employees on first start.

### Development

```bash
dotnet run --project backend/src/AppHost       # Aspire: Postgres + API + Vite dev server + dashboard (logs/traces)
```

or run the pieces yourself: `dotnet run --project backend/src/Api` (needs `ConnectionStrings__appdb` and `Jwt__Key`) and
`cd frontend && npm ci && npm run dev`.

### Tests

```bash
dotnet test backend/EmployeePayroll.slnx                     # unit + architecture + API integration (needs Docker)
dotnet test backend/src/BuildingBlocks/BuildingBlocks.slnx   # the vendored platform building blocks
cd frontend && npm test
```

---

## Architecture

```
frontend (React/TS/SCSS) ──/api──▶ Api host (composition root only)
                                     ├─ Employees module   core │ contracts │ infrastructure
                                     ├─ Payroll module     core │ contracts │ infrastructure ── uses Employees.Contracts only
                                     ├─ Identity module    core │ infrastructure
                                     └─ BuildingBlocks: Core · Data · Web  (+ Messaging · Grpc · Storage · Caching, vendored for growth)
                                   PostgreSQL: one database, one schema per module
```

- **Modular monolith, not microservices** (ADR-001). One bounded context doesn't justify a gateway, a broker and a
  cache. The modules keep service-grade boundaries anyway: other modules can only see a module's `Contracts`, each
  module owns its schema, and NetArchTest rules fail the build if a module core references EF Core or ASP.NET.
- **Clean/hexagonal inside each module** (ADR-003). The `core` project (domain + use cases + ports) references only
  `BuildingBlocks.Core`, so business rules can't reach the framework, the database or HTTP. Adapters (EF, endpoints)
  live in `Infrastructure`. The `Api` project only composes.
- **Request flow**: thin endpoint → mediator pipeline (logging → exception logging → FluentValidation) → handler
  returning `Result` → mapped to HTTP status / RFC 7807 ProblemDetails. One global exception handler, no try/catch in handlers.
- **Payroll** is a pure, ordered pipeline of rules (gross → social insurance → progressive tax → other deductions)
  driven by validated configuration (ADR-006). A new deduction type is one class.
- **Money** is `decimal` / `numeric(18,2)`. Every payslip line is rounded to 2dp half away from zero, and totals are sums of
  rounded lines, so a payslip always adds up to the cent (ADR-007).
- **Auth**: JWT with roles and permission claims, enforced server-side. An Employee can read only their own payslip (ADR-008).

Full design: [`.claude/docs/architecture.md`](.claude/docs/architecture.md) · decisions:
[`.claude/docs/adr/`](.claude/docs/adr/README.md) · API: [`.claude/docs/api-contract.md`](.claude/docs/api-contract.md) ·
payroll spec: [`.claude/docs/payroll-rules.md`](.claude/docs/payroll-rules.md) · growth path:
[`.claude/docs/scalability.md`](.claude/docs/scalability.md)

## Assumptions

- Brackets are **monthly** and apply to taxable income = gross − social insurance (configurable). Upper bounds are inclusive.
- Social insurance = rate × **base** salary, capped by a monthly maximum contribution (optional).
- Allowances are fixed monthly and taxable. "Other deductions" are recurring post-tax amounts stored on the employee.
- No proration: an employee hired on any day of a month gets that month's full pay. Months before the hire month → 422.
- Inactive employees get no payslip (422). Future months are allowed (preview).
- A payslip whose deductions exceed gross pay is refused (422) instead of producing a negative net.
- "Hire date not in the future" is compared against today's date in **UTC**.
- Email uniqueness is case-insensitive and ignores soft-deleted employees.
- Single currency (configured), used for display only.

## Known limitations / next steps

- Payslips are computed on demand, not stored, so **any month (including past ones) uses today's salary data and today's
  policy**. Next: a `PayrollRun` aggregate with immutable payslip snapshots.
- No refresh tokens, and the token lives in sessionStorage. Next: the template's BFF pattern (HttpOnly cookie,
  server-held JWT, refresh tokens).
- Search uses `ILIKE`. Next: a `pg_trgm` index for large tables. Pagination is offset-based.
- No proration or mid-month salary changes.
- See [`.claude/docs/scalability.md`](.claude/docs/scalability.md) for how messaging/outbox, gRPC and service
  extraction plug in.

## AI usage

This project was built with **Claude Code** (Opus 5.5) orchestrating several sub-agents. Everything is documented in
[`.claude/`](.claude/): the design docs written before any code, the orchestration plan (which agent owned what and
why), the log of every phase, the review cycles and their verified findings, the test reports, and the tools, skills
and instructions used ([`.claude/ai-usage/`](.claude/ai-usage/README.md)). I reviewed, ran and can explain every line.
