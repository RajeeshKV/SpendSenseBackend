# SpendSense Backend Software Design Specification (SDS)

> Version: 1.0

------------------------------------------------------------------------

# 1. Vision

SpendSense is a production-grade personal finance platform that imports
bank and credit card statements, normalizes transactions, categorizes
spending, generates analytics, and provides AI-powered financial
insights while preserving user privacy.

This document serves as the master backend specification.

------------------------------------------------------------------------

# 2. Tech Stack

-   .NET 8 Web API
-   Clean Architecture
-   EF Core
-   PostgreSQL (Supabase)
-   JWT + Refresh Tokens
-   Brevo Transactional Email
-   Supabase Storage
-   Render Deployment
-   Serilog
-   FluentValidation
-   Health Checks
-   Background Hosted Services
-   OpenAPI / Swagger
-   AI Provider Abstraction (Groq, Gemini, OpenRouter)

------------------------------------------------------------------------

# 3. Solution Structure

``` text
SpendSense.sln
src/
 ├── SpendSense.Api
 ├── SpendSense.Application
 ├── SpendSense.Domain
 ├── SpendSense.Infrastructure
 └── SpendSense.Shared

tests/
 ├── UnitTests
 └── IntegrationTests
```

## Responsibilities

### Api

-   Controllers
-   Middleware
-   Authentication
-   Swagger
-   ProblemDetails
-   Health endpoints

### Application

-   Commands
-   Queries
-   DTOs
-   Validators
-   Interfaces
-   Mapping
-   Services

### Domain

-   Entities
-   Enums
-   Value Objects
-   Domain Events
-   Specifications

### Infrastructure

-   EF Core
-   PostgreSQL
-   Brevo
-   Supabase Storage
-   AI Providers
-   Statement Parsers

------------------------------------------------------------------------

# 4. Cross Cutting Concerns

-   Correlation IDs
-   Global Exception Middleware
-   Structured Logging (Serilog)
-   Rate Limiting
-   API Versioning
-   Audit Logging
-   Pagination
-   Validation
-   Caching
-   Health Checks

------------------------------------------------------------------------

# 5. Authentication

Features

-   Register
-   Login
-   Logout
-   Refresh Token
-   Email Verification
-   Forgot Password
-   Reset Password

Security

-   Access Token: 15 min
-   Refresh Token: 30 days
-   PasswordHasher
-   Rotation of Refresh Tokens

------------------------------------------------------------------------

# 6. Brevo Integration

Emails

-   Verify Email
-   Forgot Password
-   Password Reset
-   Monthly Spending Report
-   Budget Alert
-   Subscription Confirmation

Interface

``` csharp
IEmailService
SendEmailAsync()
SendTemplateAsync()
```

Configuration

-   ApiKey
-   SenderName
-   SenderEmail
-   TemplateIds

Retry failed sends using background jobs.

------------------------------------------------------------------------

# 7. Database Design

## Core Tables

-   Users
-   RefreshTokens
-   Accounts
-   Statements
-   Transactions
-   Categories
-   MerchantMappings
-   Budgets
-   BudgetAlerts
-   Insights
-   RecurringPayments
-   EmailSubscriptions
-   Tags
-   TransactionTags
-   AuditLogs

### Common Columns

-   Id (UUID)
-   CreatedOnUtc
-   ModifiedOnUtc
-   CreatedBy
-   IsDeleted
-   RowVersion

### Important Indexes

Transactions

-   UserId + Date
-   CategoryId
-   Merchant
-   Hash UNIQUE

MerchantMappings

-   UserId + Merchant UNIQUE

------------------------------------------------------------------------

# 8. Statement Import Pipeline

``` text
Upload
 ↓
Supabase Storage
 ↓
Detect File
 ↓
Detect Bank
 ↓
Choose Parser
 ↓
Extract Transactions
 ↓
Normalize
 ↓
Duplicate Detection
 ↓
Merchant Learning
 ↓
Categorization
 ↓
Persist
 ↓
Dashboard Cache
 ↓
AI Insights
```

Supported

-   CSV
-   PDF
-   XLSX

------------------------------------------------------------------------

# 9. Parser Framework

Interface

``` text
IStatementParser
 CanParse()
 Parse()
 DetectBank()
```

Implementations

-   CsvParser
-   HdfcParser
-   IciciParser
-   AxisParser
-   SBIParser
-   KotakParser
-   FederalParser

Every parser returns the same normalized DTO.

------------------------------------------------------------------------

# 10. Merchant Normalization

Examples

AMZN MKTPLACE Amazon India Amazon Pay

↓

Amazon

Learning priority

1 User mapping

2 Built-in rules

3 AI suggestion

4 Uncategorized

------------------------------------------------------------------------

# 11. AI Architecture

Interface

``` text
IAiService

GenerateInsights()

Analyze()

Chat()
```

Never send:

-   Account numbers
-   User names
-   Addresses
-   Raw statements

Send only aggregated financial data.

Cache AI results.

Providers

-   Groq
-   Gemini
-   OpenRouter

------------------------------------------------------------------------

# 12. Dashboard Engine

Widgets

-   Total Spend
-   Total Income
-   Savings
-   Largest Category
-   Monthly Trend
-   Top Merchants
-   Cash Flow
-   Daily Spending
-   Budget Utilization

------------------------------------------------------------------------

# 13. Background Jobs

Daily

-   Refresh analytics cache

Weekly

-   Detect subscriptions

Monthly

-   AI Insights
-   Brevo email report
-   Budget alerts

Cleanup

-   Delete abandoned uploads
-   Remove expired refresh tokens

------------------------------------------------------------------------

# 14. REST API Modules

Auth

/api/auth/\*

Statements

/api/statements/\*

Transactions

/api/transactions/\*

Dashboard

/api/dashboard/\*

Budgets

/api/budgets/\*

AI

/api/ai/\*

Profile

/api/profile/\*

Settings

/api/settings/\*

------------------------------------------------------------------------

# 15. Security

-   JWT
-   HTTPS
-   Ownership validation
-   File validation
-   MIME validation
-   Antivirus hook (future)
-   CSP
-   CORS
-   Request throttling

------------------------------------------------------------------------

# 16. Performance

-   Pagination
-   Projection queries
-   Compiled EF queries
-   Response caching
-   Memory cache
-   Async everywhere
-   Batch inserts

------------------------------------------------------------------------

# 17. Logging

Serilog sinks

-   Console
-   File
-   Seq (future)

Log

-   Uploads
-   Emails
-   AI
-   Authentication
-   Exceptions

------------------------------------------------------------------------

# 18. Testing

Unit

-   Services
-   Validators
-   Parsers

Integration

-   APIs
-   Authentication
-   PostgreSQL

------------------------------------------------------------------------

# 19. Deployment

Frontend

Vercel

Backend

Render

Database

Supabase

Storage

Supabase Storage

Secrets

Render Environment Variables

------------------------------------------------------------------------

# 20. Milestones

Phase 1 - Authentication - Upload - CSV Parsing - Dashboard

Phase 2 - PDF Parsing - Merchant Learning - Budgets

Phase 3 - AI - Email Reports - Background Jobs

Phase 4 - OCR - Family Accounts - AI Chat - Forecasting

------------------------------------------------------------------------

# 21. Future Enhancements

-   Receipt OCR
-   Investment tracking
-   Loan tracking
-   Goal planning
-   Net worth timeline
-   Tax summaries
-   Open Banking integrations
-   Mobile app
-   Push notifications

------------------------------------------------------------------------

# 22. Coding Standards

-   Feature-based organization
-   Dependency Injection
-   SOLID
-   Async APIs
-   XML documentation
-   DTO-only API contracts
-   ProblemDetails errors
-   API versioning
-   Consistent response envelope

------------------------------------------------------------------------

# 23. Suggested Folder Structure

``` text
Features/
 Auth/
 Dashboard/
 Statements/
 Transactions/
 Budgets/
 Categories/
 AI/
 Email/
 Users/
 Common/
```

------------------------------------------------------------------------

# 24. Definition of Done

-   Feature implemented
-   Validation added
-   Logging added
-   Unit tests
-   Integration tests
-   Swagger updated
-   Documentation updated
-   Performance reviewed
-   Security reviewed

This document is the baseline architecture and implementation guide for
the SpendSense backend.
