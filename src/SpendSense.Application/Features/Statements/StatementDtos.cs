using FluentValidation;
using SpendSense.Domain.Enums;

namespace SpendSense.Application.Features.Statements;

public sealed record StatementUploadRequest(Guid AccountId, string AccountName, string BankName, AccountType AccountType);
public sealed record StatementResponse(Guid Id, Guid AccountId, string OriginalFileName, string ParseStatus, string? ParserName, DateTime UploadedOnUtc, int TransactionCount);

public sealed class StatementUploadRequestValidator : AbstractValidator<StatementUploadRequest>
{
    public StatementUploadRequestValidator()
    {
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(120);
    }
}
