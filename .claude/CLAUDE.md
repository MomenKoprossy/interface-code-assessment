# Employee Management & Payroll: project instructions

Built on our **DotnetPlayGround** platform template (modular monolith variant). Read before changing code:

1. [`docs/architecture.md`](docs/architecture.md): layers, module rules, request flow (authoritative)
2. [`docs/api-contract.md`](docs/api-contract.md): the frozen HTTP contract shared by backend and frontend
3. [`docs/payroll-rules.md`](docs/payroll-rules.md): payroll formula, configuration, rounding, edge cases
4. [`docs/adr/`](docs/adr/): why things are the way they are
5. [`docs/frontend-design.md`](docs/frontend-design.md), [`docs/testing-strategy.md`](docs/testing-strategy.md), [`docs/scalability.md`](docs/scalability.md)

## Layout

```
backend/   EmployeePayroll.slnx · src/{Api, AppHost, ServiceDefaults, BuildingBlocks/*, Modules/{Employees,Payroll,Identity}/*} · tests/*
frontend/  React + TS + Vite + SCSS
.claude/   docs (design), process (how it was built: agents, reviews, test runs), ai-usage (transparency)
```

## Rules

- **Module cores** (`Modules/<M>/<M>`) reference only `BuildingBlocks.Core` and other modules' `*.Contracts`. No EF, no
  ASP.NET, no `DateTime.UtcNow` (use `IClock`). Architecture tests enforce this.
- **Endpoints are thin**: bind → `ISender.Send` → `ToHttpResult()`. No logic, no try/catch.
- Expected failures return `Result`/`Error`. Never throw for them.
- **Money is `decimal`**, rounded only through the payroll rounding helper. Never `double`.
- **Central Package Management**: never put `Version=` on a `PackageReference`. Add versions to `backend/Directory.Packages.props`.
- Every new library needs a one-line justification in `docs/adr/` or `docs/frontend-design.md`.
- **No secrets in git.** Config secrets come from env vars (`.env`, gitignored); `.env.example` documents them.
- Comments only where the code isn't self-explanatory.
- Tests ship with the change: business rules → unit tests with boundary values; new endpoint → integration test.

## Commands

```bash
dotnet build backend/EmployeePayroll.slnx
dotnet test  backend/EmployeePayroll.slnx                         # integration suite needs Docker (Testcontainers)
dotnet test  backend/src/BuildingBlocks/BuildingBlocks.slnx
dotnet run --project backend/src/AppHost                          # Aspire: postgres + api + vite + dashboard (Docker)
cd frontend && npm ci && npm run dev | npm test | npm run build | npm run lint
docker compose up --build                                         # full stack on http://localhost:8080
```

## Gotchas (inherited from the template)

- `dotnet test --nologo` breaks the Microsoft.Testing.Platform runner. Omit it.
- New migration: `dotnet ef migrations add <Name> --project backend/src/Api --output-dir Persistence/Migrations`.
- Aspire teardown: kill `AppHost.exe`/`dcp.exe` too, not only `dotnet.exe`, and `docker rm -f` its containers.
