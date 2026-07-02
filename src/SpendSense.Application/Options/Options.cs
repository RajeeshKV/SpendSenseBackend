namespace SpendSense.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SpendSense";
    public string Audience { get; set; } = "SpendSenseApi";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";
    public string ApiKey { get; set; } = string.Empty;
    public string SenderName { get; set; } = "SpendSense";
    public string SenderEmail { get; set; } = "noreply@spendsense.local";
    public long VerifyEmailTemplateId { get; set; }
    public long PasswordResetTemplateId { get; set; }
    public long MonthlyReportTemplateId { get; set; }
    public long BudgetAlertTemplateId { get; set; }
}

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";
    public string Url { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string StorageBucket { get; set; } = "statements";
}

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Folder { get; set; } = "spendsense/statements";
}

public sealed class AiOptions
{
    public const string SectionName = "AI";
    public string Provider { get; set; } = "None";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public bool StatementParsingEnabled { get; set; }
    public int StatementParsingMaxChars { get; set; } = 60000;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const long DefaultMaxUploadBytes = 10 * 1024 * 1024;
    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;
    public string Provider { get; set; } = "Supabase";
    public string BackupProvider { get; set; } = "Cloudinary";
    public string LocalPath { get; set; } = "uploads";
}

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public string AllowedOriginsCsv { get; set; } = string.Empty;

    public string[] AllowedOrigins => AllowedOriginsCsv
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

