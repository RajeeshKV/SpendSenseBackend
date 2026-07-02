using Microsoft.Extensions.DependencyInjection;
using SpendSense.Application.Features.Ai;
using SpendSense.Application.Features.Auth;
using SpendSense.Application.Features.Budgets;
using SpendSense.Application.Features.Dashboard;
using SpendSense.Application.Features.Statements;
using SpendSense.Application.Features.Transactions;

namespace SpendSense.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IStatementService, StatementService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IAiInsightService, AiInsightService>();
        return services;
    }
}
