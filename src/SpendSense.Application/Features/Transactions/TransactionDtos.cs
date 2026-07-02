using SpendSense.Domain.Enums;

namespace SpendSense.Application.Features.Transactions;

public sealed record TransactionQuery(int Page = 1, int PageSize = 25, Guid? CategoryId = null, string? Merchant = null, DateOnly? From = null, DateOnly? To = null, string? Search = null, string? SortBy = null, string? SortDirection = null);
public sealed record TransactionResponse(Guid Id, DateOnly Date, string Description, string Merchant, decimal Amount, DebitCredit DebitCredit, Guid? CategoryId, string? CategoryName, bool UserCategoryOverride);
public sealed record UpdateTransactionCategoryRequest(Guid CategoryId);
public sealed record UpdateTransactionTagRequest(string Tag);
