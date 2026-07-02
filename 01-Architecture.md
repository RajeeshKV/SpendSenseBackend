# SpendSense Backend Architecture

## Solution Structure

``` text
SpendSense.sln
│
├── src
│   ├── SpendSense.Api
│   ├── SpendSense.Application
│   ├── SpendSense.Domain
│   ├── SpendSense.Infrastructure
│   └── SpendSense.Shared
│
├── tests
│   ├── SpendSense.UnitTests
│   └── SpendSense.IntegrationTests
```

### API

Controllers, Middleware, Authentication, Swagger, Health Checks.

### Application

Use Cases, DTOs, Validators, Interfaces, Mapping, Services.

### Domain

Entities, Enums, Value Objects, Domain Events, Interfaces.

### Infrastructure

EF Core, PostgreSQL, Brevo Email, Supabase Storage, AI Providers,
Statement Parsers.

## Cross Cutting

-   JWT + Refresh Tokens
-   Serilog
-   Global Exception Middleware
-   Correlation ID
-   FluentValidation
-   ProblemDetails responses
-   IConfiguration via IOptions
-   Dependency Injection
-   Health Checks
-   Background Hosted Services

## Folder Conventions

Features should be organized by feature rather than technical layer
where practical.

Example:

Transactions/ - Commands - Queries - Validators - DTOs - Handlers
