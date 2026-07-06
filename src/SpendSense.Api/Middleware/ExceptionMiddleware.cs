using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using SpendSense.Shared;

namespace SpendSense.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(exception,
                "Unable to write error response because the response has already started. Method: {Method}, Path: {Path}, CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
            return false;
        }

        var error = ResolveError(exception, context.RequestAborted.IsCancellationRequested);
        LogException(context, exception, error.StatusCode);

        context.Response.Clear();
        context.Response.StatusCode = (int)error.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(error.Message, error.Errors, context.TraceIdentifier),
            cancellationToken);

        return true;
    }

    private static ApiError ResolveError(Exception exception, bool requestAborted) => exception switch
    {
        OperationCanceledException when requestAborted => new ApiError((HttpStatusCode)499, "Request was cancelled by the client."),
        ValidationException validationException => new ApiError(HttpStatusCode.BadRequest, "Validation failed.", validationException.Errors.Select(x => x.ErrorMessage)),
        BadHttpRequestException badHttpRequestException => new ApiError((HttpStatusCode)badHttpRequestException.StatusCode, badHttpRequestException.Message),
        UnauthorizedAccessException unauthorizedAccessException => new ApiError(HttpStatusCode.Unauthorized, unauthorizedAccessException.Message),
        KeyNotFoundException keyNotFoundException => new ApiError(HttpStatusCode.NotFound, keyNotFoundException.Message),
        InvalidOperationException invalidOperationException => new ApiError(HttpStatusCode.BadRequest, invalidOperationException.Message),
        _ => new ApiError(HttpStatusCode.InternalServerError, "An unexpected error occurred.")
    };

    private void LogException(HttpContext context, Exception exception, HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        var userId = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception,
                "Request failed with unhandled exception. StatusCode: {StatusCode}, Method: {Method}, Path: {Path}, QueryString: {QueryString}, UserId: {UserId}, CorrelationId: {CorrelationId}",
                status,
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString.Value,
                userId,
                context.TraceIdentifier);
            return;
        }

        logger.LogWarning(exception,
            "Request failed with handled exception. StatusCode: {StatusCode}, Method: {Method}, Path: {Path}, QueryString: {QueryString}, UserId: {UserId}, CorrelationId: {CorrelationId}",
            status,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString.Value,
            userId,
            context.TraceIdentifier);
    }

    private sealed record ApiError(HttpStatusCode StatusCode, string Message, IEnumerable<string>? Errors = null);
}
