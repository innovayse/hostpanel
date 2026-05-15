namespace Innovayse.Infrastructure;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Admin.Servers.Interfaces;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Services;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.Services;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Notifications.Interfaces;
using Innovayse.Domain.Products.Interfaces;
using Innovayse.Domain.Servers.Interfaces;
using Innovayse.Domain.Services.Interfaces;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Auth;
using Innovayse.Infrastructure.Billing;
using Innovayse.Infrastructure.Security;
using Innovayse.Infrastructure.Clients;
using Innovayse.Infrastructure.Domains;
using Innovayse.Infrastructure.Domains.NameAm;
using Innovayse.Infrastructure.Domains.Namecheap;
using Innovayse.Infrastructure.Notifications;
using Innovayse.Infrastructure.Persistence;
using Innovayse.Infrastructure.Plugins;
using Innovayse.Infrastructure.Products;
using Innovayse.Infrastructure.Provisioning.CPanel;
using Innovayse.Infrastructure.Repositories;
using Innovayse.Infrastructure.Servers;
using Innovayse.Infrastructure.Services;
using Innovayse.Infrastructure.Settings;
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
        // Encryption
        var encryptionKey = configuration["EncryptionKey"];
        if (!string.IsNullOrEmpty(encryptionKey))
        {
            var encryptionService = new AesEncryptionService(encryptionKey);
            services.AddSingleton<IEncryptionService>(encryptionService);
            EncryptionServiceHolder.Instance = encryptionService;
        }

        // EF Core
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

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

        // Auth services
        services.AddSingleton<TokenRevocationCache>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserService, UserService>();

        // Client services
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientUserRepository, ClientUserRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IClientAccessService, ClientAccessService>();

        // Product services
        services.AddScoped<IProductGroupRepository, ProductGroupRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

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
        services.AddScoped<IProvisioningProvider, CPanelProvisioningProvider>();
        services.AddScoped<Innovayse.Domain.Services.Interfaces.IProvisioningProvider, NullProvisioningProvider>();

        // Billing services
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentGateway, NullPaymentGateway>();

        // Support
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IKbArticleRepository, KbArticleRepository>();

        // Notifications
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
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

        // CWP API client
        services.AddHttpClient<Innovayse.SDK.Plugins.ICwpApiClient, Innovayse.Providers.CWP.CwpApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Domains
        services.AddScoped<IDomainRepository, DomainRepository>();
        services.AddScoped<IRegistrarProvider, NameAmRegistrarProvider>();
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
