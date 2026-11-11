# Scalability and growth path

> The brief asks for a solution that can grow: new deduction types, new tax rules, new modules such as
> leave or attendance. This page maps each kind of growth to the seam that already exists for it, and
> to the platform capability (vendored, not yet wired) that takes over when needed.

## What is wired today, and what is vendored for later

| Building block | Status | Why |
|---|---|---|
| `BuildingBlocks.Core` | **wired** | Result/Error, mediator pipeline, validation behavior, DDD primitives, `IClock`, `ICurrentUser`, paging |
| `BuildingBlocks.Data` | **wired** | `AppDbContextBase` (audit, soft delete, domain-event dispatch), `IUnitOfWork`, `IModuleModelConfiguration`, `IDataSeeder`. Also the generic `IRepository<T,TId>`/`EfRepository`, registered open-generic and available, though today's modules use their own ports |
| `BuildingBlocks.Web` | **wired** | `AddPlatform`/`UsePlatform`, ProblemDetails + global exception handler, `Result → IResult`, JWT + permission authz, rate limiting, security headers |
| `BuildingBlocks.Messaging` | vendored, **not referenced by the app** | Rebus/RabbitMQ integration events + EF **transactional outbox**. Becomes relevant when a module is extracted and events must cross a process boundary reliably |
| `BuildingBlocks.Grpc` | vendored, not referenced | Typed gRPC clients with service discovery + resilience. Becomes relevant when a `*.Contracts` interface is served remotely |
| `BuildingBlocks.Storage` | vendored, not referenced | `IFileStorage` (local / S3 / MinIO). Becomes relevant for payslip PDFs or employee documents |
| `BuildingBlocks.Caching` | vendored, not referenced | HybridCache via `AddPlatformCaching()` (L1 memory, Redis L2 when a `cache` connection string exists). Split out of `.Data` during code review so the app graph carries no cache/Redis packages. Becomes relevant when read volume justifies it |

The unused blocks build and test in their own solution (`backend/src/BuildingBlocks/BuildingBlocks.slnx`, run in
CI). They are **not** referenced by `EmployeePayroll.slnx`, so the running app contains no library it doesn't use
(per the brief's "every library must be used for a good reason").

## Growth scenarios

| Scenario | Seam that exists now | Steps |
|---|---|---|
| New post-tax deduction type (pension, health) | `IPayrollRule` pipeline | One rule class that appends to other-deductions + one registration line (order = pipeline order). Proven by a unit test with a `PensionRule`. A *pre-tax* deduction or a new payslip section also needs a new `PayslipDraft`/`Payslip` field (an API change) |
| New / changed tax brackets | `Payroll:TaxBrackets` config, validated on start | Edit config. No code. Applies to every month computed afterwards, including past months (no history until `PayrollRun`) |
| Different rules per country or entity | `PayrollPolicy` is a value passed into the calculator | Add an `IPayrollPolicyProvider` that selects a policy per employee |
| New module (Leave, Attendance) | Module template: `X` + `X.Contracts` + `X.Infrastructure`; `IModuleModelConfiguration`; architecture tests | Copy the Employees module layout and give it its own schema. In the Api: `AddXModule()` / `MapXEndpoints()`, `platform.HandlerAssemblies.Add(XAssembly.Reference)`, and `new XModelConfiguration()` in `DesignTimeAppDbContextFactory` (a guard test fails if the design-time model drifts from the runtime one). Add arch-test rules |
| Leave must reduce payroll (unpaid leave) | `IPayrollRule` + contracts | Leave exposes `ILeaveQueries` in `Leave.Contracts`. `GetPayslipHandler` (the only place that does I/O) loads the unpaid days for the month into the draft (a new `PayslipDraft` field), and a pure `UnpaidLeaveRule` reduces gross from it. Rules stay pure singletons |
| React to employee changes (e.g. deactivate leave balance) | `EmployeeDeactivated` (in `Employees.Contracts`) is raised when an employee goes Active → Inactive and dispatched after SaveChanges (an integration test proves a handler receives it) | Add an `IDomainEventHandler<EmployeeDeactivated>` in the reacting module and register it in its `AddXModule()`. Deletion isn't signalled yet: add an `EmployeeRemoved` event when a module needs it |
| Extract a module into its own service | Contracts-only coupling, schema per module, Aspire AppHost | Move the module's projects into a new host, then (1) give its own DbContext the schema, (2) implement the contract with a gRPC client (`BuildingBlocks.Grpc`), (3) publish cross-boundary events via the outbox (`BuildingBlocks.Messaging`), (4) add it to the AppHost and a gateway route. The DotnetPlayGround template already shows this topology (YARP gateway, schema per service) |
| Read-heavy load | Stateless API, repository ports | Scale API replicas horizontally behind a load balancer (JWT is stateless). Reference `BuildingBlocks.Caching` and cache departments/policy reads |
| Payslip history / payroll runs | Payroll core is pure | Add a `PayrollRun` aggregate that stores computed payslips (immutable snapshot) in a `payroll` schema |

## Known ceilings (and the upgrade)

- **Single `AppDbContext`**: simple migrations, but extracting a module means splitting the context. Upgrade:
  per-module DbContext with its own migrations-history table (the template's schema-per-service pattern).
- **Search uses `ILIKE '%…%'`-style matching**: fine for thousands of rows. Upgrade: a `pg_trgm` GIN index, or
  full-text search.
- **Offset pagination**: fine for HR lists. Upgrade: keyset pagination if lists grow large.
- **JWT without refresh tokens, token in sessionStorage**: acceptable for the assessment. Upgrade: the template's
  BFF pattern (HttpOnly cookie, server-held JWT, refresh tokens) from its Identity service + Dashboard.
