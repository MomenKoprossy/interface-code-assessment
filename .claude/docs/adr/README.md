# Architecture Decision Records

Short format: context → decision → consequences. Status of all: **Accepted** (2026-11-09).

---

## ADR-001: Modular monolith, not microservices

**Context.** Our DotnetPlayGround template is a microservices platform (YARP gateway, three services, gRPC,
RabbitMQ, Redis). The brief is one bounded context (employees + payroll), is time-boxed, and says "a smaller scope
done well beats a large scope done poorly". The template's own playbook says: *CRUD SaaS → 1 service + a module per
aggregate; no gRPC/messaging at first*.

**Decision.** One API host with three modules (Employees, Payroll, Identity). The modules keep service-grade boundaries:
contracts-only coupling, a schema per module, and enforcement through architecture tests.

**Consequences.** One process, one database, one `docker compose up`. No gateway, broker or cache to run. Extracting
a module later is a mechanical change (see `scalability.md`) because the boundaries already exist.

## ADR-002: Vendor the BuildingBlocks as source, wire only what's used

**Context.** The template consumes `BuildingBlocks.*` as `.nupkg` files from an in-repo feed. Reviewers can't read
binaries, and our error handling and architecture scores depend on that code. The user wants the scalability options to
exist for future use, but the brief says every library must be justified.

**Decision.** All six blocks are copied as source under `backend/src/BuildingBlocks` with `ProjectReference`s. The app
references Core, Data and Web. Messaging, Grpc, Storage and Caching (split out of Data in review round 1) build and test in
their own `BuildingBlocks.slnx` and are not part of the app's dependency graph. The packaging and local feed are removed.

**Consequences.** Every line that runs is readable in the repo. The growth-path blocks are visible, compiled and tested,
with zero runtime footprint. Changes to blocks done for this app (below) are recorded in `.claude/process/`.

## ADR-003: A module is split into Core / Contracts / Infrastructure projects

**Context.** The brief requires business rules separate from framework, DB and HTTP. Folder conventions don't stop
an accidental `using Microsoft.EntityFrameworkCore`.

**Decision.** `<Module>` (domain + application, references only BuildingBlocks.Core), `<Module>.Contracts` (the public
interface for other modules), and `<Module>.Infrastructure` (EF + HTTP adapters). NetArchTest rules guard the edges.

**Consequences.** More projects (8 module projects), but purity is enforced by the compiler and tests. Unit tests reference
only the core project, so they can't touch a database.

## ADR-004: Single AppDbContext, schema per module

**Decision.** One context in the Api host. Modules register `IModuleModelConfiguration`, own a schema, and migrations live in the Api.

**Consequences.** Simple migrations, one connection, and one unit of work across modules. The trade-off: splitting a module out
later needs its own context (documented upgrade). Chosen over per-module contexts because `AddPlatformRepositories<TContext>`
binds `IUnitOfWork` (and the generic repository) to one context, and several contexts would need a keyed redesign that buys
nothing at this size.

## ADR-005: Result + ProblemDetails for errors, no try/catch in handlers

**Decision.** Expected failures are `Result`/`Error` values (`Validation`, `NotFound`, `Conflict`, `Forbidden`,
`Unprocessable`). The web layer maps them to RFC 7807 ProblemDetails. Input validation is FluentValidation in the mediator
pipeline. Unexpected exceptions go to one `IExceptionHandler`.

**Platform changes made for this app** (recorded so they can be upstreamed to the template):
1. `Error` gets an optional `Field`, so a 409 duplicate email carries `errors.email` and the UI can show it on the field.
2. Validation `errors` keys are camelCase property paths (`allowances[0].amount`), matching the JSON the client sent.
3. `ResultExtensions` gets `ToCreatedHttpResult(...)` (201 + Location) and `ToNoContentHttpResult()` (204).
4. `GlobalExceptionHandler` becomes extensible through `IExceptionProblemMapper`. The app maps the Postgres unique violation
   (race between two creates with the same email) to 409 without a try/catch.
5. `ICurrentUser.FindClaim(type)`, used by the payslip ownership rule.

## ADR-006: Payroll as a pure rule pipeline driven by configuration

**Decision.** `PayrollCalculator` runs ordered `IPayrollRule`s over a draft. Brackets, SI rate and cap come from the
validated `Payroll` options. The calculator is a pure function of (policy, employee profile, month), and payslips are not
persisted.

**Consequences.** It's trivially unit-testable. A new *post-tax* deduction type is one rule class plus one registration line (a unit test proves it with a `PensionRule`). A pre-tax deduction or a new payslip section needs a draft/contract field. There's no payroll history: past months are recomputed from current data (documented limitation).

## ADR-007: Money as `decimal`, line-item rounding half away from zero

**Decision.** C# `decimal`, DB `numeric(18,2)`, at most 2 decimals on input (validated). Every payslip line is rounded to 2dp
`AwayFromZero`, and totals are sums of rounded lines. The UI never does money arithmetic. It formats the server's
numbers with `Intl.NumberFormat`.

**Why.** `decimal` is base-10 exact for currency values. Line-item rounding makes the payslip add up visibly.
JSON numbers with ≤2 decimals round-trip exactly through JS doubles for display purposes.

## ADR-008: JWT with roles + permissions, Identity as a module

**Decision.** A minimal Identity module: a `users` table, ASP.NET Core's `PasswordHasher` (PBKDF2), and HS256 JWTs with
`role`, `employee_id` and `permission` claims. Authorization uses the template's permission policies (`RequirePermission`)
plus a resource rule (an Employee may read only their own payslip), enforced in the payroll handler. The signing key comes
only from the environment, and the app fails at startup if it's missing or shorter than 32 bytes.

**Consequences.** It meets the brief's bonus option 2. Refresh tokens and the BFF cookie pattern are documented as next
steps (both already exist in the template).

## ADR-009: React + Vite + hand-written SCSS, no UI kit

**Decision.** See `frontend-design.md`. The template's Blazor/MudBlazor dashboard is **not** used: the brief requires
React/Angular/Vue with TypeScript and CSS/SCSS.

## ADR-010: PostgreSQL; docker compose for reviewers, Aspire for development

**Decision.** Postgres (the template's provider). `docker compose up --build` runs db + api + web (nginx proxies `/api`, so
there's no CORS). The Aspire AppHost stays for development (dashboard with traces and logs). Integration tests use Testcontainers.

**Consequences.** Reviewers need only Docker. Developers get the template's observability for free.

## ADR-011: Testing pyramid

**Decision.** Unit tests for all business rules (isolated: substituted ports, pinned clock), architecture tests for the
boundaries, a thin layer of API integration tests through the real pipeline against real Postgres, and frontend component
tests. See `testing-strategy.md`.
