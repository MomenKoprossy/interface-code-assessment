# BuildingBlocks (vendored from the DotnetPlayGround platform template)

| Block | Used by the app? | Purpose |
| --- | --- | --- |
| `BuildingBlocks.Core` | **yes** | Result/Error, paging, repository/clock/current-user abstractions, DDD primitives (Entity, AggregateRoot, value objects, domain events), hand-rolled mediator + pipeline behaviors (logging, exception logging, FluentValidation). No infrastructure dependencies. |
| `BuildingBlocks.Data` | **yes** | EF Core `AppDbContextBase` (audit stamping, soft delete + global filter, domain-event dispatch after save), `EfRepository`, `EfUnitOfWork`, `IModuleModelConfiguration`, `IDataSeeder`. |
| `BuildingBlocks.Web` | **yes** | `AddPlatform`/`UsePlatform`, ProblemDetails + global exception handler (+ `IExceptionProblemMapper`), `Result` → `IResult`, JWT bearer, permission authorization, rate limiting, security headers. |
| `BuildingBlocks.Caching` | growth path | `AddPlatformCaching()`: HybridCache (L1 memory, Redis L2 when a `cache` connection string exists). |
| `BuildingBlocks.Messaging` | growth path | `IEventBus` over RabbitMQ (Rebus) + EF transactional outbox. |
| `BuildingBlocks.Grpc` | growth path | gRPC server + typed clients bound to service-discovery names with resilience. |
| `BuildingBlocks.Storage` | growth path | `IFileStorage` with Local and S3-compatible providers. |

In the template these are packed to a local NuGet feed. Here they are **source + `ProjectReference`** so every line
that runs can be read and reviewed (ADR-002). `BuildingBlocks.slnx` builds and tests all seven.

## Changes made for this app (candidates to upstream to the template)

1. `Error.Field`: a failure can name the input field it is about (409 duplicate email → `errors.email`).
2. `ResultExtensions.ToCreatedHttpResult` (201 + Location) and `ToNoContentHttpResult` (204). Failures with a field
   carry `errors`.
3. `GlobalExceptionHandler`: validation error keys are camelCase JSON paths; pluggable `IExceptionProblemMapper`;
   expected rejections are logged at Information instead of Error.
4. `ICurrentUser.FindClaim(type)`, for resource-ownership rules.
5. `IModuleModelConfiguration` + `IDataSeeder` (BuildingBlocks.Data), for modular-monolith composition.
6. Packaging/local feed removed. ServiceDefaults trimmed to what this app uses, and health endpoints mapped in every
   environment (container healthchecks).
