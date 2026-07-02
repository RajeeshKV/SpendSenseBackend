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

public sealed class StatementService(IAppDbContext db, IStorageService storage, IOptions<StorageOptions> storageOptions) : IStatementService
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
        else if (!string.IsNullOrWhiteSpace(request.BankName) && !account.BankName.Equals(request.BankName, StringComparison.OrdinalIgnoreCase))
        {
            account.BankName = request.BankName;
        }

        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        var storagePath = await storage.SaveAsync(memory, fileName, contentType, cancellationToken);

        var statement = new Statement
        {
            UserId = userId,
            Account = account,
            OriginalFileName = fileName,
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = length,
            ParseStatus = StatementParseStatus.Processing,
            ParserName = "Queued",
            FailureReason = null
        };
        db.Statements.Add(statement);
        await db.SaveChangesAsync(cancellationToken);

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
}
