# SpendSense Backend Environment Variables

Configure these in Render before deploying.

## Required

| Variable | Example | Purpose |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | `Host=...;Port=5432;Database=postgres;Username=postgres;Password=...;SSL Mode=Require;Trust Server Certificate=true` | PostgreSQL/Supabase connection used by EF Core. |
| `Jwt__Secret` | `use-a-random-64-character-secret-value-here` | JWT signing key. Must be at least 32 characters. |
| `Jwt__Issuer` | `SpendSense` | JWT issuer. |
| `Jwt__Audience` | `SpendSenseApi` | JWT audience. |

## Strongly Recommended

| Variable | Example | Purpose |
| --- | --- | --- |
| `Cors__AllowedOrigins__0` | `https://your-frontend.vercel.app` | Frontend origin allowed to call the API. Add `__1`, `__2` for more. |
| `RUN_EF_MIGRATIONS` | `true` | Runs the bundled EF migration before app start. |
| `Storage__MaxUploadBytes` | `10485760` | Max statement upload size. |

## Supabase Storage

| Variable | Purpose |
| --- | --- |
| `Supabase__Url` | Supabase project URL. |
| `Supabase__ServiceRoleKey` | Service role key for storage operations. |
| `Supabase__StorageBucket` | Statement upload bucket, defaults to `statements`. |

The current implementation saves uploads locally as a safe fallback. Replace `LocalStorageService` with a Supabase adapter when storage credentials are ready.

## Brevo Email

| Variable | Purpose |
| --- | --- |
| `Brevo__ApiKey` | Brevo transactional email API key. |
| `Brevo__SenderName` | Sender display name. |
| `Brevo__SenderEmail` | Verified sender email. |
| `Brevo__VerifyEmailTemplateId` | Verify email template id. |
| `Brevo__PasswordResetTemplateId` | Password reset template id. |
| `Brevo__MonthlyReportTemplateId` | Monthly report template id. |
| `Brevo__BudgetAlertTemplateId` | Budget alert template id. |

## AI Provider

| Variable | Purpose |
| --- | --- |
| `AI__Provider` | `Groq`, `Gemini`, `OpenRouter`, or `None`. |
| `AI__ApiKey` | Provider API key. |
| `AI__Model` | Provider model name. |
| `AI__Endpoint` | Optional provider endpoint override. |

Only aggregated financial data should be sent to AI providers. Never send account numbers, raw statements, names, addresses, or other personal identifiers.
