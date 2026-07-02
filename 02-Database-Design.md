# Database Design

## Core Tables

Users Accounts Statements Transactions Categories MerchantMappings
Budgets RecurringPayments Insights EmailSubscriptions RefreshTokens
AuditLogs

## Important Indexes

Transactions - (UserId, Date) - (CategoryId) - (Merchant) - Unique(Hash)

MerchantMappings - Unique(UserId, Merchant)

Statements - (UserId, UploadedOn)

## Relationships

User ├─ Accounts ├─ Statements ├─ Budgets ├─ MerchantMappings └─
Transactions

Statement └─ Transactions

Category └─ Transactions

Every table should include: - Id (UUID) - CreatedOnUtc - ModifiedOnUtc -
IsDeleted (optional)
