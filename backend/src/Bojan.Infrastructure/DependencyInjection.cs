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
using Microsoft.Extensions.Options;

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
        services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        services.AddScoped<IPasswordResetTokenStore, EfPasswordResetTokenStore>();

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
        services.AddScoped<IStoreStatusQueries, StoreStatusQueries>();
        services.AddScoped<ILiveChatQueries, LiveChatQueries>();

        // --- repositories (Phases 4, 5 writes, 7) ---
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<IStockAlertRepository, StockAlertRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<ILiveChatRepository, LiveChatRepository>();

        // --- Phase 8 adapters ---
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IBackupArchiver, LocalBackupArchiver>();
        // The sandbox approves every payment without contacting a bank, so the
        // one thing that must never happen is a deployment configured for a
        // real gateway quietly getting this instead. SandboxPaymentGateway's
        // own remarks claimed that was already gated; it was registered
        // unconditionally, which meant setting Payment:GatewayUrl changed
        // nothing and orders would be marked paid for money nobody took.
        //
        // Refusing to start is the only safe answer while no real adapter
        // exists: falling back would be exactly the silent substitution this
        // guards against. Same treatment as Jwt:SigningKey above.
        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .Validate(
                payment => string.IsNullOrWhiteSpace(payment.GatewayUrl),
                "Payment:GatewayUrl is set, but no real payment gateway is implemented — the only " +
                "IPaymentGateway available is the sandbox, which approves every payment without " +
                "contacting a bank. Clear Payment:GatewayUrl to run against the sandbox deliberately, " +
                "or implement and register the real adapter before setting it.")
            .ValidateOnStart();

        // Handed to the application layer as a plain object rather than an
        // IOptions<T>: that project deliberately references nothing but the
        // domain, and a settings class is not a reason to give it its first
        // package dependency. Bound through the options system all the same, and
        // then unwrapped — binding it here, against the configuration as it
        // stands while services are still being registered, meant any source
        // added afterwards was invisible to it. Production populates
        // configuration before this runs so nothing was wrong there, but it made
        // the setting unreachable from a test host and would have silently
        // ignored a source added later for any other reason.
        services.AddOptions<WalletOptions>()
            .Bind(configuration.GetSection(WalletOptions.SectionName))
            .Validate(
                wallet => wallet.MinimumAmount >= 1 && wallet.MaximumAmount >= wallet.MinimumAmount,
                "Wallet:MinimumAmount must be at least 1 and no greater than Wallet:MaximumAmount.")
            .ValidateOnStart();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<WalletOptions>>().Value);

        services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // --- use cases ---
        services.AddScoped<AuthService>();
        services.AddScoped<CustomerPasswordService>();
        services.AddScoped<AdminAuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<BusinessService>();
        services.AddScoped<SupportService>();
        services.AddScoped<LiveChatService>();
        services.AddScoped<AdminCatalogueService>();
        services.AddScoped<AdminOperationsService>();

        // One implementation, two callers: the panel's cancel control and the
        // customer's own order screen.
        services.AddScoped<Application.Orders.OrderCancellationService>();

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
