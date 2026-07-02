using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;
using SpendSense.Domain.Entities;
using SpendSense.Domain.Enums;

namespace SpendSense.Application.Features.Statements;

public interface IStatementService
{
    Task<StatementResponse> UploadAsync(Guid userId, StatementUploadRequest request, Stream file, string fileName, string contentType, long length, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<StatementResponse> GetAsync(Guid userId, Guid statementId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid statementId, CancellationToken cancellationToken = default);
}

public sealed class StatementService(IAppDbContext db, IStorageService storage, IEnumerable<IStatementParser> parsers, IOptions<StorageOptions> storageOptions, ILogger<StatementService> logger) : IStatementService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".tsv", ".txt", ".pdf", ".xlsx", ".xlsm", ".ofx", ".qfx", ".qif", ".png", ".jpg", ".jpeg", ".webp", ".heic"
    };

    public async Task<StatementResponse> UploadAsync(Guid userId, StatementUploadRequest request, Stream file, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0 || length > storageOptions.Value.MaxUploadBytes) throw new InvalidOperationException("Uploaded statement size is not allowed.");
        if (!AllowedExtensions.Contains(Path.GetExtension(fileName))) throw new InvalidOperationException("Unsupported statement file type. Supported: csv, tsv, txt, pdf, xlsx, xlsm, ofx, qfx, qif, png, jpg, jpeg, webp, heic.");

        var account = request.AccountId == Guid.Empty
            ? null
            : await db.Accounts.FirstOrDefaultAsync(x => x.Id == request.AccountId && x.UserId == userId, cancellationToken);

        if (account is null)
        {
            account = new Account { UserId = userId, AccountName = request.AccountName, BankName = request.BankName, AccountType = request.AccountType };
            db.Accounts.Add(account);
        }

        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        var storagePath = await storage.SaveAsync(memory, fileName, contentType, cancellationToken);
        memory.Position = 0;

        var matchingParsers = parsers.Where(x => x.CanParse(fileName, contentType)).ToArray();
        var statement = new Statement
        {
            UserId = userId,
            Account = account,
            OriginalFileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = length,
            ParseStatus = matchingParsers.Length == 0 ? StatementParseStatus.Failed : StatementParseStatus.Processing,
            FailureReason = matchingParsers.Length == 0 ? "No parser is configured for this file type. Enable AI statement parsing for scanned/unknown formats." : null
        };
        db.Statements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);

        if (matchingParsers.Length > 0)
        {
            var attempts = new List<string>();
            foreach (var parser in matchingParsers)
            {
                memory.Position = 0;
                var detectedBank = "Unknown";
                try
                {
                    detectedBank = parser.DetectBank(fileName, memory);
                    memory.Position = 0;
                    logger.LogInformation("Trying statement parser {ParserName} for file {FileName}. Detected bank: {DetectedBank}.", parser.Name, fileName, detectedBank);
                    var parsed = await parser.ParseAsync(memory, cancellationToken);
                    if (parsed.Count == 0)
                    {
                        attempts.Add($"{parser.Name}:{detectedBank} extracted 0 transactions");
                        continue;
                    }

                    var inserted = await InsertTransactionsAsync(userId, account.Id, statement.Id, parsed, cancellationToken);
                    statement.ParseStatus = inserted == 0 ? StatementParseStatus.Failed : StatementParseStatus.Completed;
                    statement.ParserName = $"{parser.Name}:{detectedBank}";
                    statement.FailureReason = inserted == 0 ? $"{parser.Name} extracted transactions, but all were duplicates already imported." : null;
                    statement.ModifiedOnUtc = DateTime.UtcNow;
                    logger.LogInformation("Statement {StatementId} parsed with {TransactionCount} transactions using {ParserName}.", statement.Id, inserted, parser.Name);
                    await db.SaveChangesAsync(cancellationToken);
                    return await GetAsync(userId, statement.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    attempts.Add($"{parser.Name}:{detectedBank} failed: {ex.Message}");
                    logger.LogWarning(ex, "Statement parser {ParserName} failed for statement {StatementId} ({FileName}). Trying next parser.", parser.Name, statement.Id, fileName);
                }
            }

            statement.ParseStatus = StatementParseStatus.Failed;
            statement.FailureReason = "Unable to extract transactions. Attempts: " + string.Join("; ", attempts);
            statement.ModifiedOnUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(userId, statement.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<StatementResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Statements.Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedOnUtc)
            .Select(x => new StatementResponse(x.Id, x.AccountId, x.OriginalFileName, x.ParseStatus.ToString(), x.ParserName, x.FailureReason, x.UploadedOnUtc, x.Transactions.Count))
            .ToListAsync(cancellationToken);

    public async Task<StatementResponse> GetAsync(Guid userId, Guid statementId, CancellationToken cancellationToken = default) =>
        await db.Statements.Where(x => x.UserId == userId && x.Id == statementId && !x.IsDeleted)
            .Select(x => new StatementResponse(x.Id, x.AccountId, x.OriginalFileName, x.ParseStatus.ToString(), x.ParserName, x.FailureReason, x.UploadedOnUtc, x.Transactions.Count))
            .FirstAsync(cancellationToken);

    public async Task DeleteAsync(Guid userId, Guid statementId, CancellationToken cancellationToken = default)
    {
        var statement = await db.Statements.FirstAsync(x => x.UserId == userId && x.Id == statementId, cancellationToken);
        statement.IsDeleted = true;
        statement.ModifiedOnUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(statement.StoragePath, cancellationToken);
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
