using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation.AspNetCore;
using Serilog;
using SpendSense.Api.Middleware;
using SpendSense.Api.Services;
using SpendSense.Application;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;
using SpendSense.Infrastructure;
using SpendSense.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid request value." : error.ErrorMessage))
                .ToArray();
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SpendSense.Api.ModelValidation");
            logger.LogWarning(
                "Model validation failed. Method: {Method}, Path: {Path}, Errors: {Errors}, CorrelationId: {CorrelationId}",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                errors,
                context.HttpContext.TraceIdentifier);

            return new BadRequestObjectResult(ApiResponse<object>.Fail("Validation failed.", errors, context.HttpContext.TraceIdentifier));
        };
    });
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("api", limiter =>
{
    limiter.PermitLimit = 120;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));

var cors = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options => options.AddPolicy("Default", policy =>
{
    if (cors.AllowedOrigins.Length == 0) policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else policy.WithOrigins(cors.AllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    if (!httpContext.Request.Path.StartsWithSegments("/api") || httpContext.Response.HasStarted)
    {
        return;
    }

    var message = httpContext.Response.StatusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request.",
        StatusCodes.Status401Unauthorized => "Authentication is required.",
        StatusCodes.Status403Forbidden => "You are not allowed to access this resource.",
        StatusCodes.Status404NotFound => "Resource not found.",
        StatusCodes.Status405MethodNotAllowed => "Method not allowed.",
        StatusCodes.Status413PayloadTooLarge => "Request payload is too large.",
        StatusCodes.Status415UnsupportedMediaType => "Unsupported media type.",
        StatusCodes.Status429TooManyRequests => "Too many requests. Please try again later.",
        _ => "Request failed."
    };

    var logger = httpContext.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("SpendSense.Api.StatusCodePages");
    logger.LogWarning(
        "Request completed with error status. StatusCode: {StatusCode}, Method: {Method}, Path: {Path}, QueryString: {QueryString}, CorrelationId: {CorrelationId}",
        httpContext.Response.StatusCode,
        httpContext.Request.Method,
        httpContext.Request.Path,
        httpContext.Request.QueryString.Value,
        httpContext.TraceIdentifier);

    httpContext.Response.ContentType = "application/json";
    await httpContext.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message, null, httpContext.TraceIdentifier));
});
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapMethods("/", new[] { "GET", "HEAD" }, () => Results.Ok(new { name = "SpendSense API", status = "Live", swagger = "/swagger", health = "/health" }));
app.MapControllers().RequireRateLimiting("api");
app.Run();

public partial class Program;
