using Microsoft.AspNetCore.RateLimiting;
using FluentValidation.AspNetCore;
using Serilog;
using SpendSense.Api.Middleware;
using SpendSense.Api.Services;
using SpendSense.Application;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;
using SpendSense.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
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
app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors("Default");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => Results.Ok(new { name = "SpendSense API", status = "Live", swagger = "/swagger", health = "/health" }));
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("api");
app.Run();

public partial class Program;


