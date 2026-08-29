namespace Innovayse.Infrastructure;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Migration.Interfaces;
using Innovayse.Application.Notifications.Options;
using Innovayse.Application.Notifications.Services;
using Innovayse.Application.Support.Interfaces;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Migration.Interfaces;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.Domain.Slides.Interfaces;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Audit;
using Innovayse.Infrastructure.Auth;
using Innovayse.Infrastructure.Billing;
using Innovayse.Infrastructure.Clients;
using Innovayse.Infrastructure.Common;
using Innovayse.Infrastructure.Domains;
using Innovayse.Infrastructure.Integrations.CPanel;
using Innovayse.Infrastructure.Integrations.CPanel.Options;
using Innovayse.Infrastructure.Integrations.Migration;
using Innovayse.Infrastructure.Integrations.NameAm;
using Innovayse.Infrastructure.Integrations.NameAm.Options;
using Innovayse.Infrastructure.Integrations.Namecheap;
using Innovayse.Infrastructure.Integrations.Namecheap.Options;
using Innovayse.Infrastructure.Integrations.Stripe;
using Innovayse.Infrastructure.Integrations.Stripe.Options;
using Innovayse.Infrastructure.Integrations.Telegram;
using Innovayse.Infrastructure.Integrations.Telegram.Options;
using Innovayse.Infrastructure.Notifications;
using Innovayse.Infrastructure.Notifications.Options;
using Innovayse.Infrastructure.Orders;
using Innovayse.Infrastructure.Persistence;
using Innovayse.Infrastructure.Plugins;
using Innovayse.Infrastructure.Products;
using Innovayse.Infrastructure.Provisioning;
using Innovayse.Infrastructure.Resilience.Extensions;
using Innovayse.Infrastructure.Resilience.Options;
using Innovayse.Infrastructure.Security;
using Innovayse.Infrastructure.Servers;
using Innovayse.Infrastructure.Services;
using Innovayse.Infrastructure.Settings;
using Innovayse.Infrastructure.Slides;
using Innovayse.Infrastructure.Support;
using Innovayse.Providers.CWP;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IProvisioningProvider = Innovayse.Domain.Provisioning.Interfaces.IProvisioningProvider;

/// <summary>
/// Registers all Infrastructure layer services into the DI container.
/// Call <see cref="AddInfrastructure"/> from <c>Program.cs</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds EF Core, Identity, JWT token services, Infrastructure repositories, and discovers plugins.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="loggerFactory">Logger factory used during plugin discovery at startup.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        // Encryption.
        //
        // An absent key used to mean "skip encryption", and appsettings.json ships
        // EncryptionKey as "". Nothing failed: EncryptionServiceHolder.CreateConverter
        // returned null, the EF configurations took their `else` branch, and server
        // passwords, API tokens, access hashes and client-service passwords were
        // written to the database in plain text — on a deployment that looked healthy
        // and logged nothing.
        //
        // So outside Development it is required. Development still allows it to be
        // absent, because a developer's database holds nothing worth protecting and
        // requiring a key would only be worked around.
        var encryptionKey = configuration["EncryptionKey"];
        var isDevelopment = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            if (!isDevelopment)
                throw new InvalidOperationException(
                    "EncryptionKey is not set. Server credentials and client-service "
                    + "passwords would be stored unencrypted. Generate one with: "
                    + "openssl rand -base64 32");
        }
        else
        {
            var encryptionService = new AesEncryptionService(encryptionKey);
            services.AddSingleton<IEncryptionService>(encryptionService);
            EncryptionServiceHolder.Instance = encryptionService;
        }

        // Outbound HTTP resilience.
        //
        // Registered first because every AddHttpClient below reaches for it. Until this existed
        // not one of the eleven client registrations in this file had a retry, a breaker or a
        // timeout of its own, so a registrar or a control panel that stopped answering held a
        // request thread for HttpClient's 100-second default -- and a third party having a bad
        // ten minutes became this platform having a bad ten minutes.
        //
        // The section is optional: every profile carries the measured default in
        // HttpResilienceOptions, and an operator only names one to move it. It is validated on
        // start rather than at first use, because the first use of the cPanel profile is a
        // customer's provisioning run.
        services.AddOptions<HttpResilienceOptions>()
            .Bind(configuration.GetSection(HttpResilienceOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<HttpResilienceOptions>, HttpResilienceOptionsValidator>();

        // EF Core
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

            // Suppress PendingModelChangesWarning so MigrateAsync() works in Development
            // even when the snapshot is slightly out of sync with the model.
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Where people live decides almost everything below. Read from configuration once,
        // here, so there is one answer rather than a scattering of Auth:Mode checks that
        // can disagree.
        var ownsItsUsers = AuthMode.IsLocal(configuration["Auth:Mode"]);

        // The one DI-resolvable answer to the same question, for everything downstream
        // of service registration that used to run its own inline comparison.
        services.AddSingleton<IAuthModeProvider, ConfigurationAuthModeProvider>();

        // The local token signer, registered in both modes on purpose: the admin SPA signs in
        // with a local JWT whatever owns the accounts, so the port must resolve even where an
        // SSO issues the client portal's tokens. Singleton — it holds no per-request state.
        services.AddSingleton<Innovayse.Application.Auth.Interfaces.IJwtService, JwtTokenService>();

        if (ownsItsUsers)
        {
            // ASP.NET Core Identity — use AddIdentityCore to avoid overriding the JWT Bearer
            // authentication scheme that is configured in Program.cs.
            // AddIdentity would reset the default auth scheme to cookie-based Identity,
            // causing API endpoints to redirect to /Account/Login instead of returning 401.
            services.AddIdentityCore<AppUser>(opts =>
                {
                    opts.Password.RequiredLength = 8;
                    opts.Password.RequireDigit = false;
                    opts.Password.RequireNonAlphanumeric = false;
                    opts.User.RequireUniqueEmail = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();
        }

        // Auth services
        // Roles live in this product's own database in both modes, keyed by whatever the
        // configured identity provider calls a person. Registered outside the Identity
        // block above because it does not depend on Identity being registered at all —
        // which is the point, since SSO mode will stop registering it.
        services.AddScoped<Innovayse.Domain.Auth.Interfaces.ISubjectRoleStore, SubjectRoleStore>();

        // One provider, never both. In SSO mode ASP.NET Identity is not registered at all,
        // so there is no code path that could write a person row even by accident — which
        // is what let a second, drifting copy of every user exist before.
        if (ownsItsUsers)
        {
            services.AddScoped<Innovayse.Application.Auth.Interfaces.IIdentityProvider, LocalIdentityProvider>();
            services.AddScoped<Innovayse.Application.Auth.Interfaces.IUserProvisioning, LocalUserProvisioning>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<Innovayse.Application.Auth.Interfaces.ITwoFactorService, LocalTwoFactorService>();
        }
        else
        {
            services.AddScoped<Innovayse.Application.Auth.Interfaces.IIdentityProvider, SsoIdentityProvider>();
            services.AddScoped<Innovayse.Application.Auth.Interfaces.IUserProvisioning, SsoModeUserProvisioning>();
            services.AddScoped<Innovayse.Application.Auth.Interfaces.ITwoFactorService, SsoTwoFactorService>();

            // The SSO's service API, addressed by the same authority the token validation
            // uses. The service key is this product's own credential, not a user's.
            services.AddHttpClient<SsoServiceClient>(client =>
            {
                var authority = configuration["Sso:Authority"]
                    ?? throw new InvalidOperationException(
                        "Sso:Authority must be set when Auth:Mode is not 'local' — "
                        + "it is where this product reads its people from.");
                client.BaseAddress = new Uri(authority.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("X-Service-Key", configuration["Sso:ServiceKey"] ?? string.Empty);
            })
            // Every operation on this client is a GET that reads a person, so all of them are
            // safe to repeat. The breaker's shape -- and the fact that an open one means nobody
            // is looked up -- is argued on HttpResilienceOptions.SsoRead.
            .AddReadOnlyResilience(o => o.SsoRead);

            // The SSO's TOTP endpoints, addressed the same way — but unlike SsoServiceClient
            // above, no X-Service-Key: two-factor acts as the calling person, not as the
            // platform, so each call carries the caller's own bearer token instead (see
            // SsoTwoFactorService).
            services.AddHttpClient<SsoTwoFactorClient>(client =>
            {
                var authority = configuration["Sso:Authority"]
                    ?? throw new InvalidOperationException(
                        "Sso:Authority must be set when Auth:Mode is not 'local' — "
                        + "it is where this product reads its people from.");
                client.BaseAddress = new Uri(authority.TrimEnd('/') + "/");
            })
            // No retry stage. All three TOTP operations are POSTs that change a person's second
            // factor: enable issues a fresh secret, so a repeat hands back a different QR code
            // from the one already on screen, and verify and disable each spend a one-time code.
            .AddNoRetryResilience(o => o.SsoTwoFactor);
        }

        // Client services
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientUserRepository, ClientUserRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();

        // Read model for the client data export. Fully qualified because the Application layer's
        // Clients namespace is not imported here and importing it would shadow the Domain one.
        services.AddScoped<Innovayse.Application.Clients.Interfaces.IClientExportRepository, ClientExportRepository>();

        // Domain ownership rule for the client-facing routes. Fully qualified for the same
        // reason as the export repository above: the Application layer's Domains namespace
        // would shadow the Domain one.
        //
        // MyDomainsController no longer injects this. The eighteen client-facing My* handlers
        // call it themselves, so the check travels with the message -- which matters here
        // because every command behind those endpoints is also dispatched by the admin
        // DomainsController, and RenewDomainCommand by the auto-renew job as well.
        services.AddScoped<
            Innovayse.Application.Domains.Common.IDomainOwnership,
            Innovayse.Application.Domains.Common.DomainOwnership>();

        // The same rule for support tickets and for invoices, in the same shape.
        services.AddScoped<
            Innovayse.Application.Support.Common.ITicketOwnership,
            Innovayse.Application.Support.Common.TicketOwnership>();
        services.AddScoped<
            Innovayse.Application.Billing.Common.IInvoiceOwnership,
            Innovayse.Application.Billing.Common.InvoiceOwnership>();

        // Product services
        services.AddScoped<IProductGroupRepository, ProductGroupRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductFeatureRepository, ProductFeatureRepository>();

        // Slides
        services.AddScoped<ISlideRepository, SlideRepository>();

        // Service provisioning
        services.AddScoped<IClientServiceRepository, ClientServiceRepository>();
        services.AddScoped<ICancellationRequestRepository, CancellationRequestRepository>();
        // Bound and checked at startup: either the whole section is unset -- a deployment that
        // provisions through per-server credentials held in the database instead -- or it is
        // complete. A half-filled section used to bind silently and fail on the first call.
        services.AddOptions<CPanelOptions>()
            .Bind(configuration.GetSection(CPanelOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{CPanelOptions.SectionName} is partly configured: ApiUrl, Username and ApiToken "
                    + "must either all be set or the whole section left unset.")
            .ValidateOnStart();
        services.AddHttpClient<CPanelClient>((sp, httpClient) =>
        {
            var settings = sp.GetRequiredService<IOptions<CPanelOptions>>().Value;

            // Resolving this client at all means something is about to call WHM, so an unset
            // section is an error here even though it is allowed at startup -- said plainly and
            // naming the setting, rather than as the UriFormatException an empty URL produces.
            if (!settings.IsConfigured)
            {
                throw new InvalidOperationException(
                    $"cPanel provisioning was requested but the \"{CPanelOptions.SectionName}\" "
                    + "configuration section is not set.");
            }

            httpClient.BaseAddress = new Uri(settings.ApiUrl);

            // Kept as the outer backstop only. The resilience pipeline below decides in 45s and
            // 55s; this catches a pipeline that was configured wrong, nothing else.
            httpClient.Timeout = TimeSpan.FromSeconds(60);
            httpClient.DefaultRequestHeaders.Add(
                "Authorization",
                $"WHM {settings.Username}:{settings.ApiToken}");
        })
            // No retry stage, and the HTTP method is no guide: WHM's JSON API v1 is addressed
            // entirely over GET, so every one of the seven functions this client calls --
            // createacct, removeacct, passwd among them -- is a write dressed as a GET. A
            // method-based predicate would repeat all of them.
            .AddNoRetryResilience(o => o.CPanel);
        // Use NullCPanelProvisioningProvider as fallback for unmigrated code
        services.AddScoped<IProvisioningProvider, NullCPanelProvisioningProvider>();
        services.AddScoped<Innovayse.Domain.Services.Interfaces.IProvisioningProvider, NullProvisioningProvider>();

        // Provisioning provider factory — creates per-server providers (CWP7, cPanel, etc.)
        services.AddScoped<Innovayse.Domain.Provisioning.Interfaces.IProvisioningProviderFactory, Innovayse.Infrastructure.Provisioning.ProvisioningProviderFactory>();

        // Orders
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Billing services
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IBillableItemRepository, BillableItemRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IPaymentGateway, NullPaymentGateway>();

        // Stripe
        // Optional in the same way cPanel is: no Stripe section at all is a deployment that takes
        // no card payments, and StripeService fails on the first call that needs a key. A section
        // with only some of the keys filled in is refused here instead.
        services.AddOptions<StripeOptions>()
            .Bind(configuration.GetSection(StripeOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{StripeOptions.SectionName} is partly configured: SecretKey must be set, or the "
                    + "whole section left unset.")
            .ValidateOnStart();
        services.AddScoped<IStripeService, StripeService>();

        // Payment plugins (hosted-gateway providers, e.g. Inecobank)
        services.AddHttpClient();

        // The factory's unnamed client -- what CreateClient() with no argument hands back. It is
        // used by CpanelWhmApi for per-server WHM calls and by any plugin that asks for a client
        // without naming one, so nobody knows what it calls: a plugin's POST may well be a
        // charge. Unknown means unrepeatable, and no breaker, since every caller addresses a
        // different host. AddHttpClient() above only registers the factory; the empty name is
        // what configures the default client itself.
        services.AddHttpClient(string.Empty)
            .AddNoRetryResilience(o => o.Default);

        // The Inecobank gateway plugin's own named client.
        //
        // The plugin asks the factory for its plugin id, and until this existed that name matched
        // no registration -- and IHttpClientFactory answers an unknown name with a default client
        // rather than an error. So the one payment gateway in this product was also the last
        // 100-second path in it: no handler, no retry, no breaker, and a bank having a bad
        // afternoon holding a checkout thread for a minute and a half.
        //
        // Registered here rather than in the provider because the provider has no composition
        // root of its own: PluginLoader reflects it out of plugins/ and Infrastructure
        // deliberately does not reference it, so the client name is the whole contract between
        // the two ends. It is spelled once, on HttpClientResilienceExtensions.
        //
        // Retries only getOrderStatusExtended.do. register.do and refund.do are POSTs to the same
        // host over the same verb, so the method separates nothing and the predicate reads the
        // endpoint out of the path -- a repeated refund.do is a second refund. The timeout, and
        // why this money-moving client gets a breaker when the others do not, are argued on
        // HttpResilienceOptions.Inecobank.
        services.AddHttpClient(HttpClientResilienceExtensions.InecobankClientName, client =>
        {
            // Outer backstop only. The pipeline below decides in 15s and 40s; this catches a
            // pipeline that was configured wrong, nothing else.
            client.Timeout = TimeSpan.FromSeconds(60);
        })
            .AddInecobankResilience();

        services.AddScoped<IPaymentPluginResolver, PaymentPluginResolver>();

        // Audit
        services.AddHttpContextAccessor();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();

        // Support
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IKbArticleRepository, KbArticleRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IKbCategoryRepository, KbCategoryRepository>();
        services.AddScoped<INetworkIssueRepository, NetworkIssueRepository>();
        services.AddScoped<IPredefinedReplyRepository, PredefinedReplyRepository>();
        services.AddScoped<IDownloadRepository, DownloadRepository>();

        // Telegram — the operator's chat channel for public contact-form enquiries.
        // Optional in the same way Stripe and Name.am are: an unset section is a deployment that
        // runs no bot, and the notifier logs that it did nothing rather than failing an enquiry
        // the mail already delivered. A half-filled section is refused here instead, because a
        // token with no chat id posts nowhere while looking configured.
        //
        // The token is a path segment of every Bot API call rather than a header, so it cannot be
        // baked into the base address at registration; the notifier reads it from options per
        // call. The base address is the API root and ends in a slash — without it, the relative
        // "bot<token>/sendMessage" would replace the last segment instead of extending the path.
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{TelegramOptions.SectionName} is partly configured: BotToken and ChatId must "
                    + "either both be set or the whole section left unset.")
            .ValidateOnStart();
        services.AddHttpClient<IContactNotifier, TelegramContactNotifier>(client =>
        {
            client.BaseAddress = new Uri("https://api.telegram.org/");

            // Short on purpose. This call sits between a delivered enquiry and the visitor's
            // answer, so a Telegram that is merely slow must not hold the response open. Now the
            // outer backstop for a pipeline that decides in 8s and 9s.
            client.Timeout = TimeSpan.FromSeconds(10);
        })
            // No retry stage. sendMessage is a POST with no idempotency key, so a repeat after a
            // lost response posts the enquiry to the operator's chat twice -- and the mail has
            // already delivered it, so the duplicate buys nothing.
            .AddNoRetryResilience(o => o.Telegram);

        // Reports
        services.AddScoped<Innovayse.Application.Reports.Interfaces.IReportRepository, Innovayse.Infrastructure.Reports.ReportRepository>();
        services.AddScoped<Innovayse.Application.Reports.Interfaces.ISslMonitoringService, Innovayse.Infrastructure.Reports.SslMonitoringService>();
        services.AddScoped<Innovayse.Application.Reports.Interfaces.IDiskUsageService, Innovayse.Infrastructure.Reports.DiskUsageService>();

        // Notifications
        // Bound, but deliberately not validated on start, unlike the integration options above.
        // Every deployed tier fills the Smtp section from a different overlay and none of them
        // fills all of it -- docker-compose.prod.yml supplies only Host, Port and Password --
        // and no tier configures the Notifications section at all. Refusing a partly filled
        // section here would refuse to start the production API, so a missing value surfaces
        // where the mail is actually sent instead. See each options class's remarks.
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName));
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName));
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IEmailLogRepository, EmailLogRepository>();
        services.AddScoped<TemplateRenderer>();

        // Settings
        services.AddScoped<ISettingRepository, SettingRepository>();

        // Servers
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IServerGroupRepository, ServerGroupRepository>();
        services.AddScoped<IServerConnectionTester, ServerConnectionTester>();
        services.AddScoped<Innovayse.Application.Servers.IServerSelector, Innovayse.Application.Servers.ServerSelector>();

        // CWP API client.
        //
        // No retry stage and no breaker. Every CWP call is a form POST and the operation lives in
        // the body's `action` field, not in the method or the path: create, suspend, unsuspend and
        // terminate all post to /v1/account, and so does the account listing behind the
        // server-info screen. A predicate cannot separate the read from the writes without
        // re-reading a consumed request body, and guessing wrong re-creates a hosting account.
        // The breaker is off because the server is a per-call argument rather than a base
        // address, so one dead node would open it for every healthy one.
        services.AddHttpClient<Innovayse.SDK.Plugins.ICwpApiClient, Innovayse.Providers.CWP.CwpApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
            .AddNoRetryResilience(o => o.Cwp);

        // CWP7 API client (named + typed so the factory can also resolve it by name).
        // Both registrations carry the same profile for the same two reasons as CWP above; the
        // named one is what ProvisioningProviderFactory resolves per server, so leaving it out
        // would have left the provisioning path -- the one that matters most -- uncovered.
        services.AddHttpClient("Cwp7", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        })
            .AddNoRetryResilience(o => o.Cwp7);
        services.AddHttpClient<Innovayse.SDK.Plugins.ICwp7ApiClient, Innovayse.Providers.CWP7.Cwp7ApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        })
            .AddNoRetryResilience(o => o.Cwp7);

        // Migration
        services.AddScoped<IMigrationJobRepository, MigrationJobRepository>();
        services.AddScoped<IMigrationLogRepository, MigrationLogRepository>();
        services.AddScoped<Innovayse.Application.Migration.Services.MigrationPullWorker>();
        services.AddScoped<IMigrationSource, MigrationSourceClient>();
        // The one client where the POST is misleading in the safe direction: the pull protocol
        // posts a signed payload and answers with data, so ping, totals and a page of records all
        // read and nothing on the far side is created or spent. Retrying everything is therefore
        // correct here. No breaker -- the source URL is a per-job argument, so one bad install
        // would fail jobs against every other.
        services.AddHttpClient("migration", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        })
            .AddReadOnlyResilience(o => o.Migration);

        // Domains
        services.AddScoped<IDomainRepository, DomainRepository>();
        services.AddScoped<ITldConfigRepository, TldConfigRepository>();
        services.AddScoped<IRegistrarProvider, NameAmRegistrarProvider>();
        services.AddScoped<IRegistrarProviderFactory, RegistrarProviderFactory>();
        services.AddScoped<Innovayse.Application.Domains.Services.RegistrarProviderResolver>();
        services.AddScoped<NameAmRegistrarProvider>();
        services.AddScoped<NamecheapRegistrarProvider>();
        // Reads and purchases on the same client, so the predicate has to tell them apart rather
        // than the client being retried wholesale. GET and PUT are repeated -- Name.am's PUT is a
        // whole-resource update of nameservers, contacts or the lock -- and POST only for the
        // availability check and the login, never for /client/carts/purchase, which registers,
        // transfers or renews a domain and bills for it.
        services.AddHttpClient<NameAmClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
            .AddNameAmResilience();
        services.AddOptions<NameAmOptions>()
            .Bind(configuration.GetSection(NameAmOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{NameAmOptions.SectionName} is partly configured: Email and Password must either "
                    + "both be set or the whole section left unset.")
            .ValidateOnStart();

        // Namecheap (kept for reference / future multi-registrar support)
        // Every Namecheap call is a GET to the same URL, so the method says nothing and the
        // operation is the Command query parameter -- which ranges from namecheap.domains.check
        // to namecheap.domains.create on the same verb. The predicate reads that parameter and
        // repeats only the lookups; anything it does not recognise counts as a write.
        services.AddHttpClient<NamecheapClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        })
            .AddNamecheapResilience();
        services.AddOptions<NamecheapOptions>()
            .Bind(configuration.GetSection(NamecheapOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{NamecheapOptions.SectionName} is partly configured: ApiUser, ApiKey and ApiUrl "
                    + "must either all be set or the whole section left unset.")
            .ValidateOnStart();

        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        PluginLoader.DiscoverAndRegister(services, pluginsRoot, loggerFactory);
        services.AddSingleton<IPluginRegistry>(sp => sp.GetRequiredService<PluginRegistry>());

        return services;
    }
}
