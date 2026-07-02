using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
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

public sealed class StatementService(IAppDbContext db, IStorageService storage, IEnumerable<IStatementParser> parsers, IOptions<StorageOptions> storageOptions) : IStatementService
{
    private static readonly string[] AllowedExtensions = [".csv", ".pdf", ".xlsx"];

    public async Task<StatementResponse> UploadAsync(Guid userId, StatementUploadRequest request, Stream file, string fileName, string contentType, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0 || length > storageOptions.Value.MaxUploadBytes) throw new InvalidOperationException("Uploaded statement size is not allowed.");
        if (!AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant())) throw new InvalidOperationException("Only csv, pdf, and xlsx statements are supported.");

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
        var parser = parsers.FirstOrDefault(x => x.CanParse(fileName, contentType));

        var statement = new Statement
        {
            UserId = userId,
            Account = account,
            OriginalFileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = length,
            ParseStatus = parser is null ? StatementParseStatus.Failed : StatementParseStatus.Processing,
            ParserName = parser?.Name,
            FailureReason = parser is null ? "No parser was available for this file type." : null
        };
        db.Statements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);

        if (parser is not null)
        {
            var parsed = await parser.ParseAsync(memory, cancellationToken);
            foreach (var item in parsed)
            {
                var merchant = NormalizeMerchant(item.Merchant);
                var mapping = await db.MerchantMappings.Include(x => x.Category).FirstOrDefaultAsync(x => x.UserId == userId && x.Merchant == merchant, cancellationToken);
                var hash = CreateHash(userId, account.Id, item.Date, item.Amount, merchant, item.Description);
                if (await db.Transactions.AnyAsync(x => x.Hash == hash, cancellationToken)) continue;
                db.Transactions.Add(new Transaction
                {
                    UserId = userId,
                    StatementId = statement.Id,
                    AccountId = account.Id,
                    Date = item.Date,
                    Description = item.Description,
                    Merchant = mapping?.NormalizedMerchant ?? merchant,
                    Amount = item.Amount,
                    DebitCredit = item.DebitCredit,
                    CategoryId = mapping?.CategoryId,
                    ConfidenceScore = mapping is null ? null : 1m,
                    Hash = hash
                });
            }
            statement.ParseStatus = StatementParseStatus.Completed;
            statement.ModifiedOnUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(userId, statement.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<StatementResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Statements.Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.UploadedOnUtc)
            .Select(x => new StatementResponse(x.Id, x.AccountId, x.OriginalFileName, x.ParseStatus.ToString(), x.ParserName, x.UploadedOnUtc, x.Transactions.Count))
            .ToListAsync(cancellationToken);

    public async Task<StatementResponse> GetAsync(Guid userId, Guid statementId, CancellationToken cancellationToken = default) =>
        await db.Statements.Where(x => x.UserId == userId && x.Id == statementId && !x.IsDeleted)
            .Select(x => new StatementResponse(x.Id, x.AccountId, x.OriginalFileName, x.ParseStatus.ToString(), x.ParserName, x.UploadedOnUtc, x.Transactions.Count))
            .FirstAsync(cancellationToken);

    public async Task DeleteAsync(Guid userId, Guid statementId, CancellationToken cancellationToken = default)
    {
        var statement = await db.Statements.FirstAsync(x => x.UserId == userId && x.Id == statementId, cancellationToken);
        statement.IsDeleted = true;
        statement.ModifiedOnUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(statement.StoragePath, cancellationToken);
    }

    private static string NormalizeMerchant(string merchant) => string.Join(' ', merchant.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string CreateHash(Guid userId, Guid accountId, DateOnly date, decimal amount, string merchant, string description)
    {
        var input = $"{userId:N}|{accountId:N}|{date:yyyy-MM-dd}|{amount:0.00}|{merchant.ToUpperInvariant()}|{description.ToUpperInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
