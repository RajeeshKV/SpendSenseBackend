using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendSense.Domain.Enums;
using SpendSense.Infrastructure.Persistence;
using SpendSense.Infrastructure.StatementParsers;

namespace SpendSense.Infrastructure.Jobs;

internal sealed class StatementProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<StatementProcessingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                await Task.Delay(processed == 0 ? IdleDelay : TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Statement processing worker failed. It will retry after a delay.");
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<StatementProcessingService>();

        var statementIds = await db.Statements
            .Where(x => !x.IsDeleted && x.ParseStatus == StatementParseStatus.Processing)
            .OrderBy(x => x.UploadedOnUtc)
            .Select(x => x.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        foreach (var statementId in statementIds)
        {
            logger.LogInformation("Queued background processing for statement {StatementId}.", statementId);
            await processor.ProcessAsync(statementId, cancellationToken);
        }

        return statementIds.Count;
    }
}
