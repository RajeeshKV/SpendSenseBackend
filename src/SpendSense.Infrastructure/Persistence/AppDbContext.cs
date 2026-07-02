using Microsoft.EntityFrameworkCore;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Common;
using SpendSense.Domain.Entities;

namespace SpendSense.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MerchantMapping> MerchantMappings => Set<MerchantMapping>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetAlert> BudgetAlerts => Set<BudgetAlert>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<RecurringPayment> RecurringPayments => Set<RecurringPayment>();
    public DbSet<EmailSubscription> EmailSubscriptions => Set<EmailSubscription>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.Entity<User>(b => { b.HasIndex(x => x.NormalizedEmail).IsUnique(); b.Property(x => x.Email).HasMaxLength(320); b.Property(x => x.FullName).HasMaxLength(160); });
        modelBuilder.Entity<RefreshToken>(b => { b.HasIndex(x => x.TokenHash).IsUnique(); b.Property(x => x.TokenHash).HasMaxLength(128); });
        modelBuilder.Entity<Account>(b => { b.HasIndex(x => new { x.UserId, x.BankName, x.AccountName }); b.Property(x => x.AccountName).HasMaxLength(160); b.Property(x => x.BankName).HasMaxLength(120); });
        modelBuilder.Entity<Statement>(b => { b.HasIndex(x => new { x.UserId, x.UploadedOnUtc }); b.Property(x => x.OriginalFileName).HasMaxLength(260); b.Property(x => x.StoragePath).HasMaxLength(600); });
        modelBuilder.Entity<Transaction>(b =>
        {
            b.HasIndex(x => new { x.UserId, x.Date });
            b.HasIndex(x => x.CategoryId);
            b.HasIndex(x => x.Merchant);
            b.HasIndex(x => x.Hash).IsUnique();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Merchant).HasMaxLength(240);
            b.Property(x => x.Hash).HasMaxLength(128);
        });
        modelBuilder.Entity<Category>(b => { b.HasIndex(x => x.Slug).IsUnique(); b.Property(x => x.Name).HasMaxLength(80); b.Property(x => x.Slug).HasMaxLength(100); });
        modelBuilder.Entity<MerchantMapping>(b => { b.HasIndex(x => new { x.UserId, x.Merchant }).IsUnique(); b.Property(x => x.Merchant).HasMaxLength(240); b.Property(x => x.NormalizedMerchant).HasMaxLength(240); });
        modelBuilder.Entity<Budget>(b => { b.Property(x => x.Amount).HasPrecision(18, 2); b.HasIndex(x => new { x.UserId, x.CategoryId, x.Period }); });
        modelBuilder.Entity<Insight>(b => { b.HasIndex(x => new { x.UserId, x.PeriodStart, x.Period }); });
        modelBuilder.Entity<RecurringPayment>(b => { b.Property(x => x.ExpectedAmount).HasPrecision(18, 2); b.HasIndex(x => new { x.UserId, x.Merchant }); });
        modelBuilder.Entity<Tag>(b => { b.HasIndex(x => new { x.UserId, x.Name }).IsUnique(); });
        modelBuilder.Entity<TransactionTag>(b => { b.HasKey(x => new { x.TransactionId, x.TagId }); });
        modelBuilder.Entity<AuditLog>(b => { b.HasIndex(x => new { x.UserId, x.CreatedOnUtc }); });

        var categories = new[]
        {
            ("Food & Dining", "food-dining", "#f97316"), ("Groceries", "groceries", "#22c55e"), ("Transport", "transport", "#3b82f6"),
            ("Shopping", "shopping", "#a855f7"), ("Bills & Utilities", "bills-utilities", "#eab308"), ("Entertainment", "entertainment", "#ec4899"),
            ("Health", "health", "#14b8a6"), ("Income", "income", "#10b981"), ("Transfers", "transfers", "#64748b"), ("Uncategorized", "uncategorized", "#94a3b8")
        };
        foreach (var (name, slug, color) in categories)
        {
            modelBuilder.Entity<Category>().HasData(new Category { Id = SeedId(slug), Name = name, Slug = slug, ColorHex = color, IsSystem = true, CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified) entry.Entity.ModifiedOnUtc = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private static Guid SeedId(string value) { var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)); return new Guid(bytes); }
}
