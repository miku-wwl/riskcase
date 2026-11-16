# RiskCase

RiskCase is an internal financial risk investigation application for reviewing synthetic transaction, fraud and anti-money-laundering alerts. It records investigation work and independent disposition decisions. It does not process payments, change balances, freeze accounts, or connect to real bank systems.

All names, accounts, references and events in the development seed are fictional. A signal is a reason to investigate; it is not proof of fraud or money laundering. The project is an engineering sample, not a regulatory certification.

## What it supports

- A paginated alert queue with search, status, category and severity filters.
- Customer, account, historical transaction and explainable signal details.
- A relationship graph built from stored account, transaction and counterparty relationships, with analyst-recorded links.
- Case assignment, investigation notes, structured assessment, independent supervisor review and disposition.
- Version-based optimistic concurrency, idempotency receipts, PostgreSQL transactions and a persistent audit timeline.
- Cookie authentication, role checks and anti-forgery validation for browser writes.
- Generated TypeScript clients from the ASP.NET Core OpenAPI document.

## Architecture

```text
backend/RiskCase.Domain          Business entities, lifecycle and invariants
backend/RiskCase.Application     CQRS handlers, validation and authorization
backend/RiskCase.Infrastructure  EF Core, PostgreSQL and Identity persistence
backend/RiskCase.Api             ASP.NET Core endpoints, cookies and OpenAPI
frontend/                        React 19, TypeScript, shadcn-style UI, TanStack Query and React Flow
test/unit/                       NUnit domain and application tests
test/integration/                PostgreSQL-backed API workflow tests
test/e2e/                        Playwright analyst and supervisor workflows
manual/                           Editable technical chapters, figures and publication tooling
```

The backend began from the official Jason Taylor CleanArchitecture v10.8.0 template. The source tag, exact upstream commit and retained template license are recorded in [the bootstrap provenance](manual/reference/CleanArchitecture-v10.8.0-provenance.md). The application dependency direction follows Domain → Application → Infrastructure/API at composition time; Domain itself has no infrastructure dependency.

## Local requirements

- .NET 10 SDK. `global.json` requests 10.0.201 and rolls forward to a later .NET 10 feature SDK.
- Node.js 20.19 or later and npm.
- PostgreSQL 15 or later, with a local database created for this application.

Use a local PostgreSQL login and password. Do not commit production credentials. The connection string in `appsettings.json` is a local sample only.

```powershell
$env:ConnectionStrings__RiskCaseDb = "Host=localhost;Port=5432;Database=riskcase;Username=postgres;Password=<local-password>"
dotnet restore RiskCase.slnx
dotnet run --project backend/RiskCase.Api/RiskCase.Api.csproj --launch-profile http
```

The API applies checked-in EF Core migrations at startup in Development and seeds four demo accounts and four synthetic cases. It does not seed demo data in Production. Open the frontend in a second terminal:

```powershell
Set-Location frontend
npm ci
npm run dev
```

Vite serves the UI at `http://localhost:5173` and proxies `/api`, `/openapi` and `/scalar` to `http://localhost:5250`. Set `RISKCASE_API_URL` to override the proxy target. The generated client uses same-origin requests and sends the authentication cookie and anti-forgery header.

The development API documentation is available at `http://localhost:5250/scalar`; the OpenAPI document is at `http://localhost:5250/openapi/v1.json`.

### Development accounts

These accounts are created only by the Development/Testing seed and are not production credentials.

| Role | Email | Password |
|---|---|---|
| Risk analyst | `analyst@riskcase.local` | `RiskAnalyst!2026` |
| Senior analyst | `senior@riskcase.local` | `RiskSenior!2026` |
| Supervisor | `supervisor@riskcase.local` | `RiskSupervisor!2026` |
| Read-only auditor | `auditor@riskcase.local` | `RiskAuditor!2026` |

Risk analysts can work on cases assigned to them and claim unassigned cases. Supervisors can assign work and make the final decision. A supervisor cannot decide a case assigned to that same user. Auditors can inspect the queue and history but cannot issue case mutations. Public self-registration creates no privileged role.

## Database and migrations

The initial migration is checked in under `backend/RiskCase.Infrastructure/Data/Migrations`. For a fresh database, Development startup applies it. To manage migrations explicitly, install the .NET 10 EF tool and use the same connection string:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.15
dotnet ef database update --project backend/RiskCase.Infrastructure/RiskCase.Infrastructure.csproj --startup-project backend/RiskCase.Api/RiskCase.Api.csproj
```

Production startup does not migrate by default. Set `RiskCase__ApplyMigrationsOnStartup=true` only when that deployment decision is intentional; otherwise apply migrations as a release operation.

## Upstream alert intake

Alert intake is intended for an upstream monitoring service. Configure a secret outside the repository:

```powershell
$env:RiskCase__IngestionApiKey = "<secret-from-your-secret-store>"
```

Send `POST /api/ingestion/alerts` with `X-Alert-Ingestion-Key` and a UUID `Idempotency-Key`. Repeated requests with the same source alert and identical payload return the existing case. Reuse of the external alert identifier with a different payload returns a conflict. The endpoint accepts synthetic or otherwise authorized source records; it does not connect to a transaction processor.

## Verification

Run the domain and application unit tests without PostgreSQL:

```powershell
dotnet test test/unit/RiskCase.Domain.UnitTests/Domain.UnitTests.csproj
dotnet test test/unit/RiskCase.Application.UnitTests/Application.UnitTests.csproj
```

PostgreSQL-backed API tests require a disposable database supplied through `ConnectionStrings__RiskCaseDb`:

```powershell
$env:ConnectionStrings__RiskCaseDb = "Host=localhost;Port=5432;Database=riskcase_test;Username=postgres;Password=<local-password>"
dotnet test test/integration/RiskCase.IntegrationTests/IntegrationTests.csproj
```

To build the backend, generate OpenAPI and rebuild the typed client and frontend:

```powershell
dotnet build backend/RiskCase.Api/RiskCase.Api.csproj
Set-Location frontend
npm ci
npm run build
```

`npm run build` runs NSwag before TypeScript checking and the production Vite build. The generated source is checked in at `frontend/src/generated/riskcase-api.ts` so API drift is visible in review.

Playwright uses a real PostgreSQL instance and launches the API plus Vite:

```powershell
Set-Location test/e2e
npm ci
npx playwright install chromium
$env:ConnectionStrings__RiskCaseDb = "Host=localhost;Port=5432;Database=riskcase_e2e;Username=postgres;Password=<local-password>"
npm test
```

GitHub Actions runs backend/unit/integration checks, frontend/API-client generation checks, and the browser workflow against separate PostgreSQL service databases.

## Security and data boundaries

- Browser writes to case and Identity routes require an anti-forgery token; case writes also require `If-Match` and `Idempotency-Key`.
- `RiskCase.Version` is an EF concurrency token. Stale writes return HTTP 412 rather than silently overwriting another investigation.
- Idempotency receipts store request fingerprints and serialized results in the same database transaction as the business change.
- Customer account numbers are masked, audit events avoid copying note text into log fields, and domain seed information is fictional.
- API access depends on explicit role policies. Hidden buttons are not authorization controls.
- A production deployment still needs environment-specific secrets, TLS termination, user provisioning and an operational privacy/security review.

## Textbook

The editable Chinese-language textbook, original vector figures, source map and reproducible publication scripts live under [`manual/`](manual/). The final illustrated edition is published at `manual/dist/RiskCase-Financial-Risk-Management-Engineering-Textbook.pdf`.
