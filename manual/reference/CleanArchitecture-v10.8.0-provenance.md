# Jason Taylor CleanArchitecture v10.8.0 provenance

The RiskCase backend was initialized from the official `JasonTaylorDev/CleanArchitecture` repository, tag `v10.8.0`, commit `e5c08ecead8a52f6cf519477bf6c57668248c035`.

## Bootstrap record

- Source repository: <https://github.com/jasontaylordev/CleanArchitecture>
- Source tag and commit: `v10.8.0` / `e5c08ecead8a52f6cf519477bf6c57668248c035`
- Template package: `Clean.Architecture.Solution.Template` version `10.8.0`
- Template command: `dotnet new ca-sln --client-framework React --database postgresql --output <temporary-bootstrap-directory> --no-update-check`
- Generated application projects: `Domain`, `Application`, `Infrastructure`, and `Web`; the actual projects were copied into the `backend/RiskCase.*` layout and renamed to `RiskCase.*`.
- Template source license: MIT. The upstream license text is preserved in [CleanArchitecture-MIT-License.txt](CleanArchitecture-MIT-License.txt).

The exact source commit was checked from the downloaded upstream checkout before the generated solution was copied. The bootstrap started from the template's complete generated solution rather than handwritten project files. RiskCase retains the actual four-project dependency graph, registration extensions, MediatR pipeline pattern, FluentValidation integration, EF Core context and configuration discovery, Identity integration, exception handling, and migration initialization pattern. The sample Todo domain and unrelated sample endpoints were removed after the framework mechanisms had been identified.

## Adaptation boundary

`RiskCase.Domain` owns the banking investigation rules and has no project reference to the other application layers. `RiskCase.Application` depends on Domain and defines use cases plus persistence/security interfaces. `RiskCase.Infrastructure` implements those interfaces with EF Core, PostgreSQL and Identity. `RiskCase.Api` composes the layers and exposes HTTP endpoints. Unit, integration and browser-test projects remain under `test/` and are not referenced by production projects.

RiskCase additions such as optimistic concurrency, PostgreSQL advisory-lock idempotency, cookie anti-forgery, risk-case commands and the React analyst workspace are application-specific work; they are not claims about features supplied by the upstream template.
