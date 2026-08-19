namespace Innovayse.Infrastructure;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Clients.Services;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Services;
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
using Innovayse.Infrastructure.Domains.NameAm;
using Innovayse.Infrastructure.Domains.Namecheap;
using Innovayse.Infrastructure.Notifications;
using Innovayse.Infrastructure.Orders;
using Innovayse.Infrastructure.Persistence;
using Innovayse.Infrastructure.Persistence.Repositories;
using Innovayse.Infrastructure.Plugins;
using Innovayse.Infrastructure.Products;
using Innovayse.Infrastructure.Provisioning;
using Innovayse.Infrastructure.Provisioning.CPanel;
using Innovayse.Infrastructure.Repositories;
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
        var ownsItsUsers = string.Equals(
            configuration["Auth:Mode"] ?? "sso", "local", StringComparison.OrdinalIgnoreCase);

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
        }
        else
        {
            services.AddScoped<Innovayse.Application.Auth.Interfaces.IIdentityProvider, SsoIdentityProvider>();
            services.AddScoped<Innovayse.Application.Auth.Interfaces.IUserProvisioning, SsoModeUserProvisioning>();

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
            });
        }

        // Client services
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientUserRepository, ClientUserRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IClientAccessService, ClientAccessService>();

        // Product services
        services.AddScoped<IProductGroupRepository, ProductGroupRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductFeatureRepository, ProductFeatureRepository>();

        // Slides
        services.AddScoped<ISlideRepository, SlideRepository>();

        // Service provisioning
        services.AddScoped<IClientServiceRepository, ClientServiceRepository>();
        services.AddScoped<ICancellationRequestRepository, CancellationRequestRepository>();
        services.Configure<CPanelSettings>(configuration.GetSection("CPanel"));
        services.AddHttpClient<CPanelClient>((sp, httpClient) =>
        {
            var settings = sp.GetRequiredService<IOptions<CPanelSettings>>().Value;
            httpClient.BaseAddress = new Uri(settings.ApiUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(60);
            httpClient.DefaultRequestHeaders.Add(
                "Authorization",
                $"WHM {settings.Username}:{settings.ApiToken}");
        });
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
        services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
        services.AddScoped<IStripeService, StripeService>();

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

        // Reports
        services.AddScoped<Innovayse.Application.Reports.Interfaces.IReportRepository, Innovayse.Infrastructure.Reports.ReportRepository>();
        services.AddScoped<Innovayse.Application.Reports.Interfaces.ISslMonitoringService, Innovayse.Infrastructure.Reports.SslMonitoringService>();
        services.AddScoped<Innovayse.Application.Reports.Interfaces.IDiskUsageService, Innovayse.Infrastructure.Reports.DiskUsageService>();

        // Notifications
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.Configure<Innovayse.Application.Notifications.Settings.NotificationSettings>(
            configuration.GetSection("Notifications"));
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

        // CWP API client
        services.AddHttpClient<Innovayse.SDK.Plugins.ICwpApiClient, Innovayse.Providers.CWP.CwpApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // CWP7 API client (named + typed so the factory can also resolve it by name)
        services.AddHttpClient("Cwp7", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddHttpClient<Innovayse.SDK.Plugins.ICwp7ApiClient, Innovayse.Providers.CWP7.Cwp7ApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // Migration
        services.AddScoped<IMigrationJobRepository, MigrationJobRepository>();
        services.AddScoped<IMigrationLogRepository, MigrationLogRepository>();
        services.AddScoped<Innovayse.Application.Migration.Services.MigrationPullWorker>();
        services.AddHttpClient("migration", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // Domains
        services.AddScoped<IDomainRepository, DomainRepository>();
        services.AddScoped<ITldConfigRepository, TldConfigRepository>();
        services.AddScoped<IRegistrarProvider, NameAmRegistrarProvider>();
        services.AddScoped<IRegistrarProviderFactory, RegistrarProviderFactory>();
        services.AddScoped<Innovayse.Application.Domains.Services.RegistrarProviderResolver>();
        services.AddScoped<NameAmRegistrarProvider>();
        services.AddScoped<NamecheapRegistrarProvider>();
        services.AddHttpClient<NameAmClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.Configure<NameAmSettings>(configuration.GetSection("NameAm"));

        // Namecheap (kept for reference / future multi-registrar support)
        services.AddHttpClient<NamecheapClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.Configure<NamecheapSettings>(configuration.GetSection("Namecheap"));

        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        PluginLoader.DiscoverAndRegister(services, pluginsRoot, loggerFactory);
        services.AddSingleton<IPluginRegistry>(sp => sp.GetRequiredService<PluginRegistry>());

        return services;
    }
}
