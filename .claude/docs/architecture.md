# Architecture

> Source of truth for how the solution is built. Every agent reads this before writing code.
> Decisions and their trade-offs are recorded as ADRs in [`adr/`](adr/).

## 1. Shape: a modular monolith on the DotnetPlayGround platform

The app is one ASP.NET Core API host made of **business modules** (Employees, Payroll, Identity). It
sits on the **platform building blocks** (`BuildingBlocks.*`), which come from our DotnetPlayGround
template and are vendored here as readable source.

```
                 ┌────────────────────────── frontend/ (React + TS + SCSS, Vite) ──────────────────────────┐
                 │  pages → feature hooks → typed API client (ProblemDetails-aware) → fetch /api/*           │
                 └──────────────────────────────────────────────┬───────────────────────────────────────────┘
                                                                │ HTTP/JSON, Bearer JWT
┌───────────────────────────────────────────── backend/src/Api (composition root) ───────────────────────────────┐
│  AddServiceDefaults (OTel logs/traces/metrics, /health /alive) · AddPlatform (ProblemDetails, global exception     │
│  handler, mediator pipeline, JWT + permission authz, rate limiting, security headers) · AppDbContext (Npgsql)    │
│  · OpenAPI + Swagger UI · migrate + seed at startup                                                               │
├───────────────┬──────────────────────────────┬──────────────────────────────┬─────────────────────────────────────┤
│ Modules       │ Employees                    │ Payroll                      │ Identity                            │
│ (business)    │  Employees        (core)     │  Payroll         (core)      │  Identity         (core)            │
│               │  Employees.Contracts (API)◀──┼── uses contract only         │  Identity.Infrastructure            │
│               │  Employees.Infrastructure    │  Payroll.Contracts (perms)   │                                     │
│               │                              │  Payroll.Infrastructure      │                                     │
├───────────────┴──────────────────────────────┴──────────────────────────────┴─────────────────────────────────────┤
│ Platform: BuildingBlocks.Core · BuildingBlocks.Data · BuildingBlocks.Web  (+ ServiceDefaults, AppHost)              │
│ Growth path, vendored but not wired: .Messaging (Rebus + outbox) · .Grpc · .Storage · .Caching (HybridCache/Redis) │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
                                         PostgreSQL — one database, one schema per module
```

## 2. Layers inside a module (Clean / hexagonal)

| Project | Contains | May reference | Must NOT reference |
|---|---|---|---|
| `<Module>` (core) | Domain (entities, value objects, domain rules) + Application (commands/queries, handlers, FluentValidation validators, **ports** = interfaces it needs) | `BuildingBlocks.Core`, other modules' `*.Contracts` | EF Core, ASP.NET Core, Npgsql, any `*.Infrastructure`, other modules' core |
| `<Module>.Contracts` | The module's public API for other modules: interfaces, DTO records, the domain events other modules may react to, and permission/claim names | `BuildingBlocks.Core` (Result, DomainEvent) | everything else |
| `<Module>.Infrastructure` | Adapters: EF Core entity configurations + repository implementations, minimal-API endpoints, options binding, seeding, the module's `Add<Module>Module()` / `Map<Module>Endpoints()` | its core, `BuildingBlocks.Data`, `BuildingBlocks.Web` | other modules' core or infrastructure |
| `Api` | Composition root only: wires modules, DbContext, OpenAPI, pipeline. No business logic. | everything | — |

`backend/tests/Architecture.Tests` enforces these rules with NetArchTest, so a violation fails the build pipeline.

**Why module cores are separate projects (not folders):** the compiler enforces purity. A module core
cannot accidentally `using Microsoft.EntityFrameworkCore` because it has no reference to it. That is the
brief's "business rules separate from the framework, the database and the HTTP layer", enforced by
the build instead of by convention. (The template's CMS kept layers as folders, and
its EF configuration sat in the `Domain` namespace. This build fixes that.)

## 3. Request flow

```
HTTP → endpoint (Infrastructure, 3 lines: bind → ISender.Send → result.ToHttpResult())
     → mediator pipeline (BuildingBlocks.Core): Logging → Validation (FluentValidation)
     → handler (module core): orchestrates domain + ports, returns Result / Result<T>
     → ports implemented in Infrastructure (EF repository, options)
     ← Result mapped to HTTP by BuildingBlocks.Web: 200/201/204, or RFC 7807 ProblemDetails 400/401/403/404/409/422
Unexpected exceptions → GlobalExceptionHandler (IExceptionHandler) → ProblemDetails 500 with traceId.
No try/catch in handlers or endpoints.
```

## 4. Cross-module communication

- **Synchronous reads**: through a `*.Contracts` interface (e.g. Payroll asks `IEmployeePayrollProfileProvider`
  for an employee's salary profile). It's implemented by the owning module and registered in DI. Payroll never sees
  the `Employee` entity or the Employees tables.
- **Side effects**: in-process **domain events** (BuildingBlocks.Core `DomainEvent`), dispatched after
  `SaveChanges` by `AppDbContextBase`. Example: `EmployeeDeactivated` (raised by the aggregate, no handler yet:
  the hook a future Leave/Attendance module subscribes to).
- **Growth path**: when a module is extracted into its own service, the contract interface gets a gRPC
  implementation (`BuildingBlocks.Grpc`) and domain events that cross the boundary become integration events
  published through the **transactional outbox** (`BuildingBlocks.Messaging`). Callers don't change. See
  [`scalability.md`](scalability.md).

## 5. Persistence

- PostgreSQL via EF Core 10 (Npgsql). **One `AppDbContext`** in the Api host; each module contributes its
  mappings through `IModuleModelConfiguration` and owns a **schema** (`employees`, `identity`). Payroll has no
  tables: payslips are computed on demand.
- Each module defines its **own repository port** in its Application layer (`IEmployeeRepository`, `IUserRepository`),
  implemented with EF in that module's Infrastructure. Only `IUnitOfWork` comes from BuildingBlocks. Handlers never touch
  `IQueryable` or EF async operators. (BuildingBlocks also offers a generic `IRepository<T,TId>`; it's available but
  today's modules don't need it.)
- Audit stamping + soft delete are inherited from `AppDbContextBase` (`IAuditable`, `ISoftDeletable`).
  Email uniqueness is a **filtered unique index** `ux_employees_email` on the normalized email `WHERE "IsDeleted" = false`
  (columns use EF's default PascalCase names). Pay items are JSON complex collections (`jsonb`) inside the employee row,
  so a soft delete never cascades to them.
- Migrations live in the Api project (single context). Applied at startup, then seeders run (`IDataSeeder`, ordered).
- Money columns are `numeric(18,2)`.

## 6. Cross-cutting

| Concern | Where |
|---|---|
| Validation | FluentValidation validators in module cores, run by the mediator `ValidationBehavior` → 400 ProblemDetails with camelCase field paths |
| Domain invariants | The rules themselves live in the domain (`EmployeeRules`: limits + predicates). The validator adds field messages on top; the `Employee` aggregate re-checks the same rules as **guards** in `Create`/`Update`, so a caller that skips validation (seeder, another module, a test) gets an `ArgumentException`, not an invalid employee. One rule source, two uses |
| Error format | RFC 7807 ProblemDetails everywhere (BuildingBlocks.Web): `type,title,status,detail,traceId,errors?` |
| AuthN | JWT bearer (HS256, key from env), issued by the Identity module |
| AuthZ | Permission claims + `RequirePermission(...)` (BuildingBlocks.Web), plus a resource rule in Payroll (an Employee may read only their own payslip), all enforced server-side. Permission names live in the owning module's `*.Contracts` (`EmployeePermissions`, `PayrollPermissions`), so Identity's role map can't drift from what endpoints check. An integration test asserts every `/api` endpoint except login declares authorization |
| Time | `IClock` everywhere; never `DateTime.UtcNow` in logic (hire-date and payroll tests pin the clock) |
| Logging | Structured logging (OpenTelemetry logs, JSON console in containers), HTTP logging (metadata only), mediator request timing |
| Health | `/health` (readiness, includes DB) and `/alive` (liveness) |
| API docs | OpenAPI document at `/openapi/v1.json` + Swagger UI at `/swagger` (Bearer auth scheme); served in Development or when `OpenApi:Enabled=true` |
| Config & secrets | `appsettings.json` holds only non-secret config (payroll policy). Secrets come from env vars / user-secrets; `.env.example` is committed and `.env` is gitignored |

## 7. Frontend

React 19 + TypeScript + Vite, plain SCSS, no UI kit. Feature folders (`features/employees`,
`features/payroll`, `features/auth`) hold hooks and pages. No business rules in components: validation rules,
money formatting and API error mapping live in plain TS modules, which the tests cover. Details:
[`frontend-design.md`](frontend-design.md).

## 8. Runtime topologies

| Mode | Command | What runs |
|---|---|---|
| Reviewer / prod-like | `docker compose up --build` | postgres + api (container) + web (nginx serving the built SPA, proxying `/api`) |
| Dev, orchestrated | `dotnet run --project backend/src/AppHost` | Aspire: postgres container + api + Vite dev server + Aspire dashboard (traces/logs) |
| Dev, manual | `dotnet run --project backend/src/Api` + `npm run dev` in frontend | needs `ConnectionStrings__appdb`, `Jwt__Key` (and `Seed__DemoPassword` for demo logins) |
