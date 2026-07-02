using SpendSense.Domain.Common;
using SpendSense.Domain.Enums;

namespace SpendSense.Domain.Entities;

public sealed class User : Entity
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? EmailVerificationTokenHash { get; set; }
    public DateTime? EmailVerificationTokenExpiresOnUtc { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresOnUtc { get; set; }
    public int TokenVersion { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresOnUtc { get; set; }
    public DateTime? RevokedOnUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public bool IsActive => RevokedOnUtc is null && ExpiresOnUtc > DateTime.UtcNow;
}

public sealed class Account : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public string? MaskedAccountNumber { get; set; }
    public ICollection<Statement> Statements { get; set; } = new List<Statement>();
}

public sealed class Statement : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedOnUtc { get; set; } = DateTime.UtcNow;
    public StatementParseStatus ParseStatus { get; set; } = StatementParseStatus.Uploaded;
    public string? ParserName { get; set; }
    public string? FailureReason { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

public sealed class Transaction : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid StatementId { get; set; }
    public Statement? Statement { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Merchant { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DebitCredit DebitCredit { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool UserCategoryOverride { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string Hash { get; set; } = string.Empty;
    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}

public sealed class Category : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
    public bool IsSystem { get; set; } = true;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

public sealed class MerchantMapping : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public string NormalizedMerchant { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
}

public sealed class Budget : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BudgetAlert : Entity
{
    public Guid BudgetId { get; set; }
    public Budget? Budget { get; set; }
    public decimal ThresholdPercent { get; set; }
    public DateTime? SentOnUtc { get; set; }
}

public sealed class Insight : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public InsightPeriod Period { get; set; } = InsightPeriod.Monthly;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

public sealed class RecurringPayment : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Merchant { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public int FrequencyDays { get; set; }
    public DateOnly? NextExpectedOn { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmailSubscription : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public EmailSubscriptionType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class Tag : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}

public sealed class TransactionTag
{
    public Guid TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }
}

public sealed class AuditLog : Entity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? MetadataJson { get; set; }
    public string? IpAddress { get; set; }
}
