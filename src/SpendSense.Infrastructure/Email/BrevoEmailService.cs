using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;

namespace SpendSense.Infrastructure.Email;

public sealed class BrevoEmailService(IOptions<BrevoOptions> options, ILogger<BrevoEmailService> logger) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            logger.LogInformation("Brevo API key not configured. Email to {Email} with subject {Subject} was skipped.", toEmail, subject);
            return Task.CompletedTask;
        }
        logger.LogInformation("Brevo email adapter configured. Implement HTTP send for {Email}.", toEmail);
        return Task.CompletedTask;
    }

    public Task SendTemplateAsync(string toEmail, long templateId, object parameters, CancellationToken cancellationToken = default) =>
        SendAsync(toEmail, $"Template {templateId}", parameters.ToString() ?? string.Empty, cancellationToken);
}
