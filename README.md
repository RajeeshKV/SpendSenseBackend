# SpendSense Backend

Clean Architecture .NET 8 backend for statement imports, transaction analytics, budgets, and AI-powered spending insights.

## Structure

- `src/SpendSense.Api` - Controllers, middleware, Swagger, auth wiring, health checks.
- `src/SpendSense.Application` - DTOs, validators, interfaces, use-case services.
- `src/SpendSense.Domain` - Entities, enums, common base types.
- `src/SpendSense.Infrastructure` - EF Core, PostgreSQL, auth, storage, email, AI, parsers, jobs.
- `src/SpendSense.Shared` - API response envelope and shared contracts.
- `tests` - Unit and integration test projects.

## Local Run

```powershell
$env:ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=spendsense;Username=postgres;Password=postgres'
$env:Jwt__Secret='local-development-secret-at-least-32-characters'
dotnet restore
dotnet ef database update --project src\SpendSense.Infrastructure --startup-project src\SpendSense.Api
dotnet run --project src\SpendSense.Api
```

Swagger is available at `/swagger` and health checks at `/health`.

## Render Deployment

The `Dockerfile` publishes the API and creates an EF Core migration bundle named `migrate`. `scripts/render-start.sh` runs migrations when `RUN_EF_MIGRATIONS=true`, then starts the API using Render's `PORT` value.

Environment variables are documented in `docs/ENVIRONMENT.md`.
