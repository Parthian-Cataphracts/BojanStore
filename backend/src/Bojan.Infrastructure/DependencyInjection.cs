using Bojan.Application.Accounts;
using Bojan.Application.Administration;
using Bojan.Application.Auth;
using Bojan.Application.Business;
using Bojan.Application.Catalogue;
using Bojan.Application.Checkout;
using Bojan.Application.Common;
using Bojan.Application.Support;
using Bojan.Infrastructure.Auth;
using Bojan.Infrastructure.Common;
using Bojan.Infrastructure.Notifications;
using Bojan.Infrastructure.Payments;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Persistence.Seed;
using Bojan.Infrastructure.Queries;
using Bojan.Infrastructure.Repositories;
using Bojan.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Bojan")
            ?? throw new InvalidOperationException(
                "Connection string 'Bojan' is not configured. Set it in appsettings.json or the " +
                "ConnectionStrings__Bojan environment variable.");

        services.AddDbContext<BojanDbContext>(options => options.UseNpgsql(connectionString));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                jwt => jwt.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters — set it in configuration or the " +
                "Jwt__SigningKey environment variable. There is no default that works in production, " +
                "matching the frontend's own AUTH_SECRET.")
            .ValidateOnStart();

        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddOptions<PaymentOptions>().Bind(configuration.GetSection(PaymentOptions.SectionName));

        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLog, AuditLog>();

        // --- auth (Phase 1) ---
        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<IAdminUserRepository, EfAdminUserRepository>();
        services.AddScoped<IOtpChallengeStore, EfOtpChallengeStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ISmsSender, ConsoleSmsSender>();

        // Random codes for everyone. Development may decorate this — see
        // AddDevelopmentSignIn, which is the only caller that ever changes it
        // and which the host only invokes in Development.
        services.AddSingleton<RandomOtpCodeGenerator>();
        services.AddSingleton<IOtpCodeGenerator>(provider => provider.GetRequiredService<RandomOtpCodeGenerator>());

        // --- queries (Phases 2, 3, 5 reads, 6) ---
        services.AddScoped<ICatalogueQueries, CatalogueQueries>();
        services.AddScoped<IAccountQueries, AccountQueries>();
        services.AddScoped<IBusinessQueries, BusinessQueries>();
        services.AddScoped<IAdminQueries, AdminQueries>();

        // --- repositories (Phases 4, 5 writes, 7) ---
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<IStockAlertRepository, StockAlertRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();

        // --- Phase 8 adapters ---
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // --- use cases ---
        services.AddScoped<AuthService>();
        services.AddScoped<AdminAuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<BusinessService>();
        services.AddScoped<SupportService>();
        services.AddScoped<AdminCatalogueService>();
        services.AddScoped<AdminOperationsService>();

        services.AddScoped<CatalogueSeeder>();

        return services;
    }

    /// <summary>
    /// Gives one configured number a fixed sign-in code, and seeds the account
    /// behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For developers with no SMS gateway. <c>Program.cs</c> calls this only
    /// when the host is in Development, so on any other environment the
    /// decorator is never constructed and <c>Auth:DevOtp</c> is never read —
    /// setting it in production configuration does nothing at all.
    /// </para>
    /// <para>
    /// Kept as its own method, called from one guarded line, so that "can this
    /// reach production?" is a question with a one-line answer.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddDevelopmentSignIn(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DevOtpOptions>().Bind(configuration.GetSection(DevOtpOptions.SectionName));

        services.AddSingleton<IOtpCodeGenerator, StaticOtpCodeGenerator>();

        return services;
    }
}
