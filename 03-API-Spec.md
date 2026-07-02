# REST API Specification

## Authentication

POST /api/auth/register POST /api/auth/login POST /api/auth/refresh POST
/api/auth/logout POST /api/auth/verify-email POST
/api/auth/forgot-password POST /api/auth/reset-password

## Statements

POST /api/statements/upload GET /api/statements GET /api/statements/{id}
DELETE /api/statements/{id}

## Transactions

GET /api/transactions GET /api/transactions/search PATCH
/api/transactions/{id}/category PATCH /api/transactions/{id}/tag

Supports pagination, filtering, sorting.

## Dashboard

GET /api/dashboard GET /api/dashboard/categories GET
/api/dashboard/monthly GET /api/dashboard/merchants

## AI

POST /api/ai/analyze GET /api/ai/latest

## Budgets

Full CRUD

All responses:

{ success, message, data, errors, correlationId }
