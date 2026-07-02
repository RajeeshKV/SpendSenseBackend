using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Transactions;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/transactions")]
public sealed class TransactionsController(ITransactionService transactions) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TransactionResponse>>>> Get([FromQuery] TransactionQuery query, CancellationToken cancellationToken) => Envelope(await transactions.GetAsync(UserId, query, cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<PagedResult<TransactionResponse>>>> Search([FromQuery] TransactionQuery query, CancellationToken cancellationToken) => Envelope(await transactions.GetAsync(UserId, query, cancellationToken));

    [HttpPatch("{id:guid}/category")]
    public async Task<ActionResult<ApiResponse<object>>> Category(Guid id, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken) { await transactions.UpdateCategoryAsync(UserId, id, request, cancellationToken); return EmptyEnvelope("Transaction category updated."); }

    [HttpPatch("{id:guid}/tag")]
    public async Task<ActionResult<ApiResponse<object>>> Tag(Guid id, UpdateTransactionTagRequest request, CancellationToken cancellationToken) { await transactions.AddTagAsync(UserId, id, request, cancellationToken); return EmptyEnvelope("Transaction tag added."); }
}
