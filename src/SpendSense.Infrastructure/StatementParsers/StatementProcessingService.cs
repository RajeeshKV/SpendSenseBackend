using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Entities;
using SpendSense.Domain.Enums;
using SpendSense.Infrastructure.Persistence;

namespace SpendSense.Infrastructure.StatementParsers;

internal sealed class StatementProcessingService(AppDbContext db, IStorageService storage, IEnumerable<IStatementParser> parsers, ILogger<StatementProcessingService> logger)
{
    public async Task ProcessAsync(Guid statementId, CancellationToken cancellationToken = default)
    {
        var statement = await db.Statements.Include(x => x.Account).FirstOrDefaultAsync(x => x.Id == statementId && !x.IsDeleted, cancellationToken);
        if (statement is null) return;

        await using var content = await storage.OpenReadAsync(statement.StoragePath, cancellationToken);
        var matchingParsers = parsers.Where(x => x.CanParse(statement.OriginalFileName, statement.ContentType)).OrderByDescending(x => x.Name.Equals("AiStatementParser", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matchingParsers.Length == 0)
        {
            statement.ParseStatus = StatementParseStatus.Failed;
            statement.FailureReason = "No parser is configured for this file type. Configure AI statement parsing for universal imports.";
            statement.ModifiedOnUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var attempts = new List<string>();
        foreach (var parser in matchingParsers)
        {
            if (content.CanSeek) content.Position = 0;
            var detectedBank = statement.Account?.BankName ?? "Unknown";
            try
            {
                var parserBank = parser.DetectBank(statement.OriginalFileName, content);
                detectedBank = string.IsNullOrWhiteSpace(statement.Account?.BankName) ? parserBank : statement.Account.BankName;
                if (content.CanSeek) content.Position = 0;

                logger.LogInformation("Background statement parser {ParserName} started for statement {StatementId}. Bank: {DetectedBank}.", parser.Name, statement.Id, detectedBank);
                var parsed = await parser.ParseAsync(content, cancellationToken);
                if (parsed.Count == 0)
                {
                    attempts.Add($"{parser.Name}:{detectedBank} extracted 0 transactions");
                    logger.LogWarning("Background statement parser {ParserName} extracted 0 transactions for statement {StatementId}.", parser.Name, statement.Id);
                    continue;
                }

                var inserted = await InsertTransactionsAsync(statement.UserId, statement.AccountId, statement.Id, parsed, cancellationToken);
                statement.ParseStatus = inserted == 0 ? StatementParseStatus.Failed : StatementParseStatus.Completed;
                statement.ParserName = $"{parser.Name}:{detectedBank}";
                statement.FailureReason = inserted == 0 ? $"{parser.Name} extracted transactions, but all were duplicates already imported." : null;
                statement.ModifiedOnUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Background statement parser {ParserName} completed statement {StatementId}. Inserted transactions: {InsertedCount}.", parser.Name, statement.Id, inserted);
                return;
            }
            catch (Exception ex)
            {
                attempts.Add($"{parser.Name}:{detectedBank} failed: {ex.Message}");
                logger.LogWarning(ex, "Background statement parser {ParserName} failed for statement {StatementId}. Trying next parser.", parser.Name, statement.Id);
            }
        }

        statement.ParseStatus = StatementParseStatus.Failed;
        statement.FailureReason = "Unable to extract transactions. Attempts: " + string.Join("; ", attempts);
        statement.ModifiedOnUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> InsertTransactionsAsync(Guid userId, Guid accountId, Guid statementId, IReadOnlyList<ParsedTransaction> parsed, CancellationToken cancellationToken)
    {
        var inserted = 0;
        foreach (var item in parsed)
        {
            var merchant = NormalizeMerchant(item.Merchant);
            var mapping = await db.MerchantMappings.Include(x => x.Category).FirstOrDefaultAsync(x => x.UserId == userId && x.Merchant == merchant, cancellationToken);
            var hash = CreateHash(userId, accountId, item.Date, item.Amount, merchant, item.Description);
            if (await db.Transactions.AnyAsync(x => x.Hash == hash, cancellationToken)) continue;
            db.Transactions.Add(new Transaction
            {
                UserId = userId,
                StatementId = statementId,
                AccountId = accountId,
                Date = item.Date,
                Description = item.Description,
                Merchant = mapping?.NormalizedMerchant ?? merchant,
                Amount = item.Amount,
                DebitCredit = item.DebitCredit,
                CategoryId = mapping?.CategoryId,
                ConfidenceScore = mapping is null ? null : 1m,
                Hash = hash
            });
            inserted++;
        }

        return inserted;
    }

    private static string NormalizeMerchant(string merchant) => string.Join(' ', merchant.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string CreateHash(Guid userId, Guid accountId, DateOnly date, decimal amount, string merchant, string description)
    {
        var input = $"{userId:N}|{accountId:N}|{date:yyyy-MM-dd}|{amount:0.00}|{merchant.ToUpperInvariant()}|{description.ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
