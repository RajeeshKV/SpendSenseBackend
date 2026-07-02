using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Statements;
using SpendSense.Domain.Enums;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/statements")]
public sealed class StatementsController(IStatementService statements) : ApiControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<ApiResponse<StatementResponse>>> Upload([FromForm] IFormFile file, [FromForm] Guid accountId, [FromForm] string accountName, [FromForm] string bankName, [FromForm] AccountType accountType, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var request = new StatementUploadRequest(accountId, accountName, bankName, accountType);
        return Envelope(await statements.UploadAsync(UserId, request, stream, file.FileName, file.ContentType, file.Length, cancellationToken), "Statement uploaded and parsed.");
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StatementResponse>>>> Get(CancellationToken cancellationToken) => Envelope(await statements.GetAsync(UserId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StatementResponse>>> Get(Guid id, CancellationToken cancellationToken) => Envelope(await statements.GetAsync(UserId, id, cancellationToken));

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<StatementResponse>>> Status(Guid id, CancellationToken cancellationToken) => Envelope(await statements.GetAsync(UserId, id, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken) { await statements.DeleteAsync(UserId, id, cancellationToken); return EmptyEnvelope("Statement deleted."); }
}
