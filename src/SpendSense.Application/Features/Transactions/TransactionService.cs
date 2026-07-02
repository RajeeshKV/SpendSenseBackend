using Microsoft.EntityFrameworkCore;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Entities;
using SpendSense.Shared;

namespace SpendSense.Application.Features.Transactions;

public interface ITransactionService
{
    Task<PagedResult<TransactionResponse>> GetAsync(Guid userId, TransactionQuery query, CancellationToken cancellationToken = default);
    Task UpdateCategoryAsync(Guid userId, Guid transactionId, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken = default);
    Task AddTagAsync(Guid userId, Guid transactionId, UpdateTransactionTagRequest request, CancellationToken cancellationToken = default);
}

public sealed class TransactionService(IAppDbContext db) : ITransactionService
{
    public async Task<PagedResult<TransactionResponse>> GetAsync(Guid userId, TransactionQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var size = query.PageSize is < 1 or > 100 ? 25 : query.PageSize;
        var transactions = db.Transactions.Include(x => x.Category).Where(x => x.UserId == userId && !x.IsDeleted);
        if (query.CategoryId.HasValue) transactions = transactions.Where(x => x.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.Merchant)) transactions = transactions.Where(x => x.Merchant.ToLower().Contains(query.Merchant.ToLower()));
        if (!string.IsNullOrWhiteSpace(query.Search)) transactions = transactions.Where(x => x.Description.ToLower().Contains(query.Search.ToLower()) || x.Merchant.ToLower().Contains(query.Search.ToLower()));
        if (query.From.HasValue) transactions = transactions.Where(x => x.Date >= query.From.Value);
        if (query.To.HasValue) transactions = transactions.Where(x => x.Date <= query.To.Value);

        transactions = query.SortBy?.ToLowerInvariant() switch
        {
            "amount" => query.SortDirection == "asc" ? transactions.OrderBy(x => x.Amount) : transactions.OrderByDescending(x => x.Amount),
            "merchant" => query.SortDirection == "asc" ? transactions.OrderBy(x => x.Merchant) : transactions.OrderByDescending(x => x.Merchant),
            _ => query.SortDirection == "asc" ? transactions.OrderBy(x => x.Date) : transactions.OrderByDescending(x => x.Date)
        };

        var total = await transactions.CountAsync(cancellationToken);
        var items = await transactions.Skip((page - 1) * size).Take(size)
            .Select(x => new TransactionResponse(x.Id, x.Date, x.Description, x.Merchant, x.Amount, x.DebitCredit, x.CategoryId, x.Category == null ? null : x.Category.Name, x.UserCategoryOverride))
            .ToListAsync(cancellationToken);
        return new PagedResult<TransactionResponse>(items, page, size, total);
    }

    public async Task UpdateCategoryAsync(Guid userId, Guid transactionId, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await db.Transactions.FirstAsync(x => x.UserId == userId && x.Id == transactionId, cancellationToken);
        var category = await db.Categories.FirstAsync(x => x.Id == request.CategoryId, cancellationToken);
        transaction.CategoryId = category.Id;
        transaction.UserCategoryOverride = true;
        db.MerchantMappings.Add(new MerchantMapping { UserId = userId, Merchant = transaction.Merchant, NormalizedMerchant = transaction.Merchant, CategoryId = category.Id });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTagAsync(Guid userId, Guid transactionId, UpdateTransactionTagRequest request, CancellationToken cancellationToken = default)
    {
        var transaction = await db.Transactions.Include(x => x.TransactionTags).FirstAsync(x => x.UserId == userId && x.Id == transactionId, cancellationToken);
        var normalized = request.Tag.Trim();
        var tag = await db.Tags.FirstOrDefaultAsync(x => x.UserId == userId && x.Name == normalized, cancellationToken);
        if (tag is null)
        {
            tag = new Tag { UserId = userId, Name = normalized };
            db.Tags.Add(tag);
        }
        if (!transaction.TransactionTags.Any(x => x.TagId == tag.Id)) transaction.TransactionTags.Add(new TransactionTag { TransactionId = transaction.Id, Tag = tag });
        await db.SaveChangesAsync(cancellationToken);
    }
}
