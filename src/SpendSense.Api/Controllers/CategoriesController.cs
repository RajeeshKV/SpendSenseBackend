using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpendSense.Application.Abstractions;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

public sealed record CategoryResponse(Guid Id, string Name, string Slug, string? ColorHex);

[Route("api/categories")]
public sealed class CategoriesController(IAppDbContext db) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryResponse>>>> Get(CancellationToken cancellationToken)
    {
        IReadOnlyList<CategoryResponse> categories = await db.Categories.Where(x => !x.IsDeleted).OrderBy(x => x.Name).Select(x => new CategoryResponse(x.Id, x.Name, x.Slug, x.ColorHex)).ToListAsync(cancellationToken);
        return Envelope(categories);
    }
}
