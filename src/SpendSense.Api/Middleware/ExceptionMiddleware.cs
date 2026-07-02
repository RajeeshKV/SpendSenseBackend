using System.Net;
using FluentValidation;
using SpendSense.Shared;

namespace SpendSense.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Validation failed.", ex.Errors.Select(x => x.ErrorMessage), context.TraceIdentifier));
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message, null, context.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message, null, context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled request failure.");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("An unexpected error occurred.", null, context.TraceIdentifier));
        }
    }
}
