using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;
using SpendSense.Infrastructure.Ai;
using SpendSense.Infrastructure.Auth;
using SpendSense.Infrastructure.Email;
using SpendSense.Infrastructure.Jobs;
using SpendSense.Infrastructure.Persistence;
using SpendSense.Infrastructure.StatementParsers;
using SpendSense.Infrastructure.Storage;

namespace SpendSense.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.Configure<SupabaseOptions>(configuration.GetSection(SupabaseOptions.SectionName));
        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required.");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, BrevoEmailService>();
        services.AddHttpClient<SupabaseStorageProvider>();
        services.AddScoped<IStorageProvider>(sp => sp.GetRequiredService<SupabaseStorageProvider>());
        services.AddScoped<IStorageProvider, CloudinaryStorageProvider>();
        services.AddScoped<IStorageProvider, LocalStorageProvider>();
        services.AddScoped<IStorageService, ResilientStorageService>();
        services.AddScoped<IAiService, ConfiguredAiService>();
        services.AddScoped<IStatementParser, CsvStatementParser>();
        services.AddScoped<IStatementParser, ExcelStatementParser>();
        services.AddScoped<IStatementParser, OfxQifStatementParser>();
        services.AddScoped<IStatementParser, PdfTextStatementParser>();
        services.AddHttpClient<AiStatementParser>();
        services.AddScoped<IStatementParser>(sp => sp.GetRequiredService<AiStatementParser>());
        services.AddHostedService<SpendSenseMaintenanceService>();
        services.AddHostedService<MonthlyInsightsService>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
        {
            throw new InvalidOperationException("Jwt__Secret must be configured and at least 32 characters long.");
        }
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        services.AddAuthorization();
        return services;
    }
}



