using Bojan.Application.Accounts;
using Bojan.Application.Administration;
using Bojan.Application.Auth;
using Bojan.Application.Business;
using Bojan.Application.Catalogue;
using Bojan.Application.Checkout;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Application.Payments;
using Bojan.Application.Support;
using Bojan.Application.Diagnostics;
using Bojan.Infrastructure.Auth;
using Bojan.Infrastructure.Common;
using Bojan.Infrastructure.Diagnostics;
using Bojan.Infrastructure.Jobs;
using Bojan.Infrastructure.Notifications;
using Bojan.Infrastructure.Payments;
using Bojan.Infrastructure.Persistence;
using Bojan.Infrastructure.Support;
using Bojan.Infrastructure.Persistence.Seed;
using Bojan.Infrastructure.Queries;
using Bojan.Infrastructure.Repositories;
using Bojan.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        services.AddDbContext<BojanDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                // A ceiling on how long one statement may hold a pooled
                // connection.
                //
                // Npgsql's default is thirty seconds, which is long enough for a
                // burst of slow queries to hold every connection in the pool at
                // once — and once the pool is empty every other request queues
                // behind it, so one unindexed search under load takes the whole
                // API down rather than one page. Fifteen seconds is far past
                // anything this application asks for legitimately (the slowest
                // real query is a report, and the reports are exported by a
                // worker) and short enough that the pool recovers on its own.
                npgsql.CommandTimeout(15);

                // Transient faults — a connection dropped mid-statement, a
                // failover — retried rather than surfaced as a 500. Bounded
                // tightly on purpose: this is for the network blinking, not for
                // riding out an outage, and a generous retry policy under load
                // is an amplifier that turns one slow database into three times
                // the traffic against it.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), null);
            }));

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                jwt => jwt.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters — set it in configuration or the " +
                "Jwt__SigningKey environment variable. There is no default that works in production, " +
                "matching the frontend's own AUTH_SECRET.")
            .ValidateOnStart();

        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddOptions<LogFileOptions>().Bind(configuration.GetSection(LogFileOptions.SectionName));

        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLog, AuditLog>();

        // Singleton and stateless: it holds a directory name and reads files, so
        // there is nothing per-request about it.
        services.AddSingleton<ILogFileReader, LogFileReader>();

        // --- auth (Phase 1) ---
        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<IAdminUserRepository, EfAdminUserRepository>();
        services.AddScoped<IOtpChallengeStore, EfOtpChallengeStore>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        // Which SMS service the shop sends through is a stored setting the
        // owner enters in the panel, so this only governs the stand-in used
        // until they have: whether it is allowed to print the message body,
        // which for a one-time code means printing the credential itself.
        //
        // No longer a startup gate. It was one while no provider existed, but a
        // gate on booting is the wrong shape once the account is entered through
        // the panel — the owner cannot reach the screen that configures SMS if
        // the process serving it refuses to start. ConsoleSmsSender logs a
        // dropped sign-in code as an error instead, which is the loudest thing
        // that does not also take the shop down.
        services.AddOptions<ConsoleSenderOptions>()
            .Bind(configuration.GetSection(ConsoleSenderOptions.SectionName));

        services.AddHttpClient(SmsIrSender.HttpClientName, client =>
        {
            // An OTP request is a request a shopper is waiting on. Without a
            // ceiling, a provider having a bad minute holds request threads
            // until this process's own timeout.
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Accept.Add(new("application/json"));
        });

        services.AddScoped<SmsSettingsStore>();
        services.AddScoped<ISmsSettingsStore>(provider => provider.GetRequiredService<SmsSettingsStore>());
        services.AddScoped<SmsSettingsService>();

        services.AddSingleton<ConsoleSmsSender>();
        services.AddSingleton<SmsIrSender>();
        services.AddSingleton<ConfiguredSmsSender>();
        services.AddSingleton<ISmsSender>(provider => provider.GetRequiredService<ConfiguredSmsSender>());
        services.AddSingleton<ISmsProbe>(provider => provider.GetRequiredService<ConfiguredSmsSender>());

        // Web Push. No provider and no account — the message goes straight to
        // the browser vendor's own push service, signed with a key pair the shop
        // generated itself, so all that is registered here is the store for that
        // pair and the sender that uses it.
        //
        // Scoped rather than singleton, unlike the SMS and email senders: it
        // reads and writes the subscription rows itself, deleting the ones a
        // push service reports as gone, so it needs the request's DbContext.
        services.AddHttpClient(WebPushSender.HttpClientName, client =>
        {
            // Nothing waits on this: a broadcast is a background job and a
            // transactional push follows a response that has already gone out.
            // The ceiling is here so one unreachable push service cannot hold a
            // dispatch worker open behind it.
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<WebPushSettingsStore>();
        services.AddScoped<IWebPushSettingsStore>(provider => provider.GetRequiredService<WebPushSettingsStore>());
        services.AddScoped<WebPushSettingsService>();
        services.AddScoped<IWebPushSender, WebPushSender>();
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        // One class behind both ports: the club's tiers and its earning rate
        // are read together and must not disagree about what a stored zero
        // means. See LoyaltyStore.
        services.AddScoped<LoyaltyStore>();
        services.AddScoped<Application.Accounts.ILoyaltyStore>(p => p.GetRequiredService<LoyaltyStore>());
        services.AddScoped<Application.Checkout.ILoyaltySettings>(p => p.GetRequiredService<LoyaltyStore>());
        services.AddScoped<Application.Accounts.LoyaltyService>();
        services.AddScoped<PushSubscriptionService>();
        services.AddScoped<ICustomerPushNotifier, CustomerPushNotifier>();

        // Email does deliver now. SmtpEmailSender uses the same account the
        // support mailbox already reads — one set of credentials, entered once
        // — and hands over to the console stand-in only when that account is
        // switched off or half-configured, which is a shop that has not
        // finished being set up rather than one that is running.
        services.AddSingleton<ConsoleEmailSender>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IPasswordResetTokenStore, EfPasswordResetTokenStore>();

        // Random codes for everyone. Development may decorate this — see
        // AddDevelopmentSignIn, which is the only caller that ever changes it
        // and which the host only invokes in Development.
        services.AddSingleton<RandomOtpCodeGenerator>();
        services.AddSingleton<IOtpCodeGenerator>(provider => provider.GetRequiredService<RandomOtpCodeGenerator>());

        // Bounded. Without a limit the cache grows until the process runs out
        // of memory, and the keys are built from slugs taken out of the URL —
        // so how far it grows is up to whoever is calling, not to how many
        // categories the shop has. See CachedCatalogueQueries for why entries
        // are counted rather than weighed.
        services.AddMemoryCache(options => options.SizeLimit = CachedCatalogueQueries.Entries);

        // --- queries (Phases 2, 3, 5 reads, 6) ---
        services.AddScoped<CatalogueQueries>();
        services.AddScoped<ICatalogueQueries, CachedCatalogueQueries>();
        services.AddScoped<IAccountQueries, AccountQueries>();
        services.AddScoped<IBusinessQueries, BusinessQueries>();
        services.AddScoped<IAdminQueries, AdminQueries>();
        services.AddScoped<IStoreStatusQueries, StoreStatusQueries>();
        services.AddScoped<ILiveChatQueries, LiveChatQueries>();

        // --- repositories (Phases 4, 5 writes, 7) ---
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICheckoutRepository, CheckoutRepository>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();

        // The B2B half's missing operation: turning a request into a priced
        // pro-forma. See QuoteIssuanceService.
        services.AddScoped<IQuotePricingSource, QuotePricingSource>();
        services.AddScoped<Application.Business.QuoteIssuanceService>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<IStockAlertRepository, StockAlertRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<ILiveChatRepository, LiveChatRepository>();

        // --- Phase 8 adapters ---
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IBackupArchiver, LocalBackupArchiver>();
        // Handed the same connection string the context was given, rather
        // than reading its own from configuration: two copies of one setting
        // is two things that can disagree, and the one that would be wrong
        // here is the one deciding which database gets backed up.
        services.AddSingleton<IDatabaseDumper>(provider => new PgDumpRunner(
            connectionString,
            provider.GetRequiredService<ILogger<PgDumpRunner>>()));
        // Which gateway takes the money is a stored setting the owner chooses in
        // the panel, not a registration — so this binds only the fallback
        // return address a developer runs against before opening that screen.
        services.AddOptions<PaymentOptions>().Bind(configuration.GetSection(PaymentOptions.SectionName));

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

        // --- payments ---
        //
        // The provider is chosen in the panel and read per call, so all three
        // pieces are registered and ConfiguredPaymentGateway picks between them
        // — see its remarks for why that decision moved out of registration.
        // One named client per provider, all with the same ceiling. A checkout is
        // a request a shopper is sitting in front of and the gateway is on the
        // other side of the public internet — without a timeout, a provider
        // having a bad minute becomes this API holding request threads until its
        // own.
        foreach (var name in new[]
        {
            ZarinPalPaymentGateway.HttpClientName,
            ZibalPaymentGateway.HttpClientName,
            IdPayPaymentGateway.HttpClientName,
        })
        {
            services.AddHttpClient(name, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Accept.Add(new("application/json"));
            });
        }

        services.AddScoped<PaymentGatewaySettingsStore>();
        services.AddScoped<IPaymentGatewaySettingsStore>(provider =>
            provider.GetRequiredService<PaymentGatewaySettingsStore>());
        services.AddScoped<IPaymentMethodSwitchStore, PaymentMethodSwitchStore>();
        services.AddScoped<IPaymentSettlementRepository, PaymentSettlementRepository>();

        services.AddSingleton<SandboxPaymentGateway>();
        services.AddSingleton<ZarinPalPaymentGateway>();
        services.AddSingleton<ZibalPaymentGateway>();
        services.AddSingleton<IdPayPaymentGateway>();
        services.AddSingleton<ConfiguredPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider =>
            provider.GetRequiredService<ConfiguredPaymentGateway>());
        services.AddSingleton<IPaymentGatewayProbe>(provider =>
            provider.GetRequiredService<ConfiguredPaymentGateway>());

        services.AddScoped<PaymentSettlementService>();
        services.AddScoped<PaymentSettingsService>();

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

        // The operators themselves. Its own service because everything on it
        // writes the table that decides who may write anything else.
        services.AddScoped<AdminUserService>();

        // The shipping tiers, which until now only the seeder could write.
        services.AddScoped<IShippingMethodStore, ShippingMethodStore>();
        services.AddScoped<ShippingSettingsService>();

        // One implementation, two callers: the panel's cancel control and the
        // customer's own order screen.
        services.AddScoped<Application.Orders.OrderCancellationService>();

        // The other place money goes back into a wallet. Its own service rather
        // than a method on AdminOperationsService, for the reason the
        // cancellation above is.
        services.AddScoped<Application.Orders.ReturnDecisionService>();

        services.AddScoped<CatalogueSeeder>();

        // Customer-facing transactional email. EmailLinks is bound from
        // configuration because the API has no other way to know the
        // storefront's address, and mail is composed on a worker where there is
        // no request to derive it from.
        services.AddOptions<EmailLinks>().Bind(configuration.GetSection(EmailLinks.SectionName));
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<EmailLinks>>().Value);
        services.AddScoped<EmailTemplates>();
        services.AddScoped<ICustomerMailer, CustomerMailer>();

        // The support mailbox. Data protection is what stores its password:
        // every other secret here is hashed, which cannot work for one that has
        // to be replayed to an IMAP server — see MailboxSettingsStore.
        services.AddDataProtection();
        services.AddScoped<MailboxSettingsStore>();
        services.AddScoped<IMailboxSettingsStore>(provider => provider.GetRequiredService<MailboxSettingsStore>());
        services.AddScoped<IMailboxService, MailboxService>();

        // Drains the report-export queue — see ReportExportWorker's remarks
        // for why this and not a new service to deploy.
        services.AddHostedService<ReportExportWorker>();

        // Sends the broadcasts the panel queues. Without it, INotificationDispatcher
        // was registered and never called — see NotificationWorker's remarks.
        services.AddHostedService<NotificationWorker>();

        // Takes the backups screen 156 asks for. Before this the request wrote
        // a JSON file naming itself and reported success — see BackupWorker.
        services.AddHostedService<BackupWorker>();

        // Catches the payments the callback missed. A shopper who pays and then
        // closes the tab never reaches the page that would confirm it — see
        // PaymentReconciliationWorker.
        services.AddHostedService<PaymentReconciliationWorker>();

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

        // A developer with no gateway reads the code off their own console, so
        // both switches are on here — and only here. Applied after the binding
        // in AddInfrastructure so it wins over whatever configuration said,
        // which is what keeps `pnpm dev` working with no extra setup while a
        // deployed host still has to opt in by name.
        services.PostConfigure<ConsoleSenderOptions>(console =>
        {
            console.AllowConsoleSenders = true;
            console.LogMessageBodies = true;
        });

        services.AddSingleton<IOtpCodeGenerator, StaticOtpCodeGenerator>();

        return services;
    }
}
