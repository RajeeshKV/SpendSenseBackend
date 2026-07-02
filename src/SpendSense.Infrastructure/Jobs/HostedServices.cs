using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendSense.Application.Abstractions;

namespace SpendSense.Infrastructure.Jobs;

public sealed class SpendSenseMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<SpendSenseMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var expired = await db.RefreshTokens.Where(x => x.ExpiresOnUtc < DateTime.UtcNow && !x.IsDeleted).ToListAsync(stoppingToken);
                foreach (var token in expired) token.IsDeleted = true;
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Maintenance completed. Expired refresh tokens marked: {Count}", expired.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Maintenance job failed.");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

public sealed class MonthlyInsightsService(ILogger<MonthlyInsightsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Monthly insights scheduler heartbeat. Full cadence can be moved to Hangfire or Quartz when scale requires it.");
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
