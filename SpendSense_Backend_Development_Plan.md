# SpendSense Backend Development Plan (.NET 8)

## Purpose

This document defines the backend architecture, modules, database
design, APIs, processing pipeline, AI integration, and implementation
roadmap for SpendSense.

The backend should be production-ready, scalable, and follow Clean
Architecture.

------------------------------------------------------------------------

# Technology Stack

-   .NET 8 Web API
-   C#
-   Entity Framework Core
-   PostgreSQL (Supabase)
-   JWT Authentication + Refresh Tokens
-   FluentValidation
-   Serilog
-   BackgroundService / Hosted Services
-   Hangfire (optional future)
-   AutoMapper
-   MediatR (optional CQRS)
-   Brevo Email API
-   Render Deployment
-   Supabase Storage

------------------------------------------------------------------------

# Architecture

``` text
API
│
├── Controllers
├── Middleware
├── Filters
│
Application
├── Commands
├── Queries
├── DTOs
├── Validators
├── Services
│
Domain
├── Entities
├── Enums
├── Interfaces
├── Value Objects
│
Infrastructure
├── Persistence
├── Authentication
├── Storage
├── Email
├── AI
├── StatementParsers
```

------------------------------------------------------------------------

# Authentication

Features

-   Register
-   Login
-   Refresh Token
-   Logout
-   Email Verification
-   Forgot Password
-   Reset Password

JWT access token: 15 minutes

Refresh token: 30 days

Passwords hashed using ASP.NET Identity PasswordHasher or BCrypt.

------------------------------------------------------------------------

# Email Integration (Brevo)

Use Brevo transactional email API.

Use templates for:

-   Verify Email
-   Password Reset
-   Monthly Spending Report
-   Budget Alert
-   Subscription Confirmation

Create an IEmailService abstraction.

``` text
IEmailService
    SendAsync(...)
    SendTemplateAsync(...)
```

Configuration:

-   ApiKey
-   Sender Name
-   Sender Email
-   Template Ids

Never hardcode API keys.

------------------------------------------------------------------------

# Database

## Tables

Users

Accounts

Statements

Transactions

Categories

MerchantMappings

UserRules

Budgets

RecurringPayments

Insights

Tags

RefreshTokens

EmailSubscriptions

AuditLogs

------------------------------------------------------------------------

# Entity Notes

## Users

Identity information.

## Accounts

Bank account or credit card.

Fields

-   UserId
-   AccountName
-   BankName
-   AccountType

## Statements

Tracks uploaded files.

Fields

-   OriginalFileName
-   StoragePath
-   UploadedOn
-   ParseStatus
-   ParserName

## Transactions

Normalized transaction table.

Fields

-   StatementId
-   AccountId
-   Date
-   Description
-   Merchant
-   Amount
-   DebitCredit
-   CategoryId
-   UserCategoryOverride
-   ConfidenceScore
-   Hash

Unique index on Hash.

------------------------------------------------------------------------

# Statement Processing Pipeline

``` text
Upload

↓

Save to Supabase Storage

↓

Detect File Type

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

Categorize

↓

Persist

↓

Generate Dashboard Metrics

↓

AI Insights
```

Processing should be asynchronous where possible.

------------------------------------------------------------------------

# Statement Parser Design

``` text
IStatementParser

CanParse()

Parse()

GetBank()
```

Implementations

-   HdfcParser
-   IciciParser
-   AxisParser
-   SBIParser
-   CsvParser

Each parser returns a common TransactionDto.

------------------------------------------------------------------------

# Categorization Engine

Order of precedence

1.  User Merchant Mapping
2.  Built-in Merchant Rules
3.  AI Suggestion (optional)
4.  Uncategorized

Unknown merchants remain uncategorized until corrected.

Persist corrections for future imports.

------------------------------------------------------------------------

# Duplicate Detection

Generate SHA256 hash using

AccountId

TransactionDate

Amount

Merchant

Description

Skip duplicates during import.

------------------------------------------------------------------------

# Dashboard Service

Calculate

-   Total Spend
-   Total Income
-   Monthly Comparison
-   Category Breakdown
-   Top Merchants
-   Spending Trends
-   Daily Spend
-   Average Daily Spend

Return optimized DTOs only.

------------------------------------------------------------------------

# AI Integration

Recommended providers

-   Groq
-   Google AI Studio
-   OpenRouter

Never send raw statements.

Only send

-   Category totals
-   Merchant totals
-   Spending trends
-   Subscription list
-   Anonymous transaction summaries

Prompt should request

-   Summary
-   Overspending
-   Savings Opportunities
-   Unusual Transactions
-   Budget Advice

Cache generated insights.

------------------------------------------------------------------------

# Background Jobs

Daily

-   Refresh dashboard metrics

Monthly

-   Generate insights
-   Send monthly report (Brevo)

Weekly

-   Detect recurring payments

------------------------------------------------------------------------

# REST APIs

## Auth

POST /api/auth/register

POST /api/auth/login

POST /api/auth/refresh

POST /api/auth/logout

POST /api/auth/forgot-password

POST /api/auth/reset-password

## Statements

POST /api/statements/upload

GET /api/statements

DELETE /api/statements/{id}

GET /api/statements/{id}/status

## Transactions

GET /api/transactions

PUT /api/transactions/{id}/category

GET /api/transactions/search

## Dashboard

GET /api/dashboard

GET /api/dashboard/categories

GET /api/dashboard/trends

GET /api/dashboard/merchants

## Budgets

CRUD endpoints

## AI

POST /api/ai/analyze

GET /api/ai/latest

------------------------------------------------------------------------

# Logging

Use Serilog.

Log

-   Login attempts
-   Uploads
-   Parsing failures
-   AI failures
-   Email failures
-   Exceptions

Correlation ID middleware.

------------------------------------------------------------------------

# Security

Validate uploaded files.

Allowed

-   pdf
-   csv
-   xlsx

Maximum upload size configurable.

Authorize every endpoint.

Use ownership validation so users access only their own data.

Never expose storage paths.

------------------------------------------------------------------------

# Configuration

appsettings

-   Jwt
-   Brevo
-   Supabase
-   Storage
-   AI
-   Logging

Use IOptions pattern.

------------------------------------------------------------------------

# Development Phases

## Phase 1

Authentication

Brevo integration

Statement upload

CSV parser

Database

Transactions

Dashboard

## Phase 2

PDF parsers

Merchant learning

Budgets

Duplicate detection

Recurring payment detection

## Phase 3

AI insights

Monthly emails

Receipt OCR

Advanced analytics

Chat assistant

------------------------------------------------------------------------

# Coding Standards

-   Async all database calls
-   Repository + Unit of Work (if adopted)
-   DTOs only across API boundary
-   Dependency Injection everywhere
-   XML documentation for public APIs
-   Global exception middleware
-   Consistent API response wrapper
-   Pagination for list endpoints
-   Version APIs from v1

Goal: Build a maintainable backend capable of supporting millions of
transactions while remaining easy to extend with new banks, AI
providers, and analytics features.
