using Microsoft.EntityFrameworkCore;
using SpendSense.Domain.Entities;

namespace SpendSense.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Statement> Statements { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Category> Categories { get; }
    DbSet<MerchantMapping> MerchantMappings { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<BudgetAlert> BudgetAlerts { get; }
    DbSet<Insight> Insights { get; }
    DbSet<RecurringPayment> RecurringPayments { get; }
    DbSet<EmailSubscription> EmailSubscriptions { get; }
    DbSet<Tag> Tags { get; }
    DbSet<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public interface ITokenService
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashToken(string token);
}

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendTemplateAsync(string toEmail, long templateId, object parameters, CancellationToken cancellationToken = default);
}

public interface IStorageService
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}

public sealed record ParsedTransaction(DateOnly Date, string Description, string Merchant, decimal Amount, Domain.Enums.DebitCredit DebitCredit);

public interface IStatementParser
{
    string Name { get; }
    bool CanParse(string fileName, string contentType);
    string DetectBank(string fileName, Stream content);
    Task<IReadOnlyList<ParsedTransaction>> ParseAsync(Stream content, CancellationToken cancellationToken = default);
}

public interface IAiService
{
    Task<string> GenerateInsightsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string> AnalyzeTransactionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string> ChatAsync(Guid userId, string prompt, CancellationToken cancellationToken = default);
}


