namespace SpendSense.Application.Features.Dashboard;

public sealed record DashboardSummary(decimal TotalSpend, decimal TotalIncome, decimal Savings, string? LargestCategory, IReadOnlyList<CategorySpend> Categories, IReadOnlyList<MonthlySpend> Monthly, IReadOnlyList<MerchantSpend> Merchants);
public sealed record CategorySpend(Guid? CategoryId, string Category, decimal Amount);
public sealed record MonthlySpend(int Year, int Month, decimal Spend, decimal Income);
public sealed record MerchantSpend(string Merchant, decimal Amount);
