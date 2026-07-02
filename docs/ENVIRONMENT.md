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
| `Cors__AllowedOriginsCsv` | `https://your-frontend.vercel.app,https://admin.yourdomain.com` | Comma-separated frontend origins allowed to call the API. Easier on Render than indexed array variables. |
| `RUN_EF_MIGRATIONS` | `true` | Runs the bundled EF migration before app start. |
| `Storage__MaxUploadBytes` | `10485760` | Max statement upload size. Defaults to 10 MB when omitted. |
| `Storage__Provider` | `Supabase` | Primary storage provider: `Supabase`, `Cloudinary`, or `Local`. |
| `Storage__BackupProvider` | `Cloudinary` | Backup provider used if the primary provider is unavailable. |

## Supabase Storage

Supabase File Storage currently includes a 1 GB quota on the Free plan. At the app default of 10 MB per statement, that is roughly 100 max-size files before storage quota pressure, not counting database and other Supabase limits.

| Variable | Purpose |
| --- | --- |
| `Supabase__Url` | Supabase project URL. |
| `Supabase__ServiceRoleKey` | Service role key for storage operations. |
| `Supabase__StorageBucket` | Statement upload bucket, defaults to `statements`. |

## Cloudinary Backup Storage

Use Cloudinary as a backup or primary file store by setting `Storage__BackupProvider=Cloudinary` or `Storage__Provider=Cloudinary`.

| Variable | Purpose |
| --- | --- |
| `Cloudinary__CloudName` | Cloudinary cloud name. |
| `Cloudinary__ApiKey` | Cloudinary API key. |
| `Cloudinary__ApiSecret` | Cloudinary API secret. |
| `Cloudinary__Folder` | Folder for uploaded statements, defaults to `spendsense/statements`. |

## Local Development Storage

| Variable | Purpose |
| --- | --- |
| `Storage__LocalPath` | Local upload directory when `Storage__Provider=Local`. Defaults to `uploads`. |

Do not use local storage as the production provider on Render because the filesystem is ephemeral.

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
