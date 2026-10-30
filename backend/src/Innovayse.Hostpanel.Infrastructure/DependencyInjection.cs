using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Auth.Settings;
using Innovayse.Hostpanel.Infrastructure.Authorization;
using Innovayse.Hostpanel.Infrastructure.Currency;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Infrastructure.Email;
using Innovayse.Hostpanel.Infrastructure.Email.Settings;
using Innovayse.Hostpanel.Infrastructure.ImageProcessing;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Settings;
using Innovayse.Hostpanel.Infrastructure.Integrations.Migration;
using Innovayse.Hostpanel.Infrastructure.Integrations.Migration.Settings;
using Innovayse.Hostpanel.Infrastructure.Integrations.NameAm;
using Innovayse.Hostpanel.Infrastructure.Integrations.NameAm.Settings;
using Innovayse.Hostpanel.Infrastructure.Integrations.Namecheap;
using Innovayse.Hostpanel.Infrastructure.Integrations.Namecheap.Settings;
using Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe;
using Innovayse.Hostpanel.Infrastructure.Integrations.Stripe.Settings;
using Innovayse.Hostpanel.Infrastructure.Integrations.Telegram;
using Innovayse.Hostpanel.Infrastructure.Integrations.Telegram.Settings;
using Innovayse.Hostpanel.Infrastructure.Monitoring;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Pdf;
using Innovayse.Hostpanel.Infrastructure.Pdf.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Infrastructure.Registrar;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Innovayse.Hostpanel.Infrastructure.Scheduling;
using Innovayse.Hostpanel.Infrastructure.Security;
using Innovayse.Hostpanel.Infrastructure.Security.Extensions;
using Innovayse.Hostpanel.Infrastructure.Security.Settings;
using Innovayse.Hostpanel.Infrastructure.Storage;
using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Innovayse.Hostpanel.Infrastructure.Time;
using Innovayse.Hostpanel.Shared.Settings;
using Innovayse.Providers.CWP;
using Innovayse.Providers.CWP7;
using Innovayse.SDK.Plugins;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Npgsql;
using IProvisioningProvider = Innovayse.Hostpanel.Application.Common.Interfaces.IProvisioningProvider;

namespace Innovayse.Hostpanel.Infrastructure;
/// <summary>
/// Registers all Infrastructure layer services into the DI container.
/// Call <see cref="AddInfrastructure"/> from <c>Program.cs</c>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// The Data Protection application name every instance of this backend sets, so all of them
    /// derive the same keys from the shared key ring in the database.
    /// </summary>
    private const string DataProtectionApplicationName = "Hostpanel";

    /// <summary>
    /// Adds EF Core, Identity, JWT token services, the Infrastructure adapters, and discovers plugins.
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
        // The key is required in every environment. An absent key used to mean "skip encryption",
        // and appsettings.json ships EncryptionKey as "": server credentials, client-service
        // passwords and two-factor secrets were then written in plain text on a deployment that
        // looked healthy and logged nothing. The context itself also refuses to be built without
        // the service, so there is no second way in.
        var encryptionKey = configuration["EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new InvalidOperationException(
                "EncryptionKey is not set. Server credentials, client-service "
                + "passwords and two-factor secrets would be stored unencrypted. "
                + "Generate one with: openssl rand -base64 32");
        }

        services.AddSingleton<IEncryptionService>(new AesEncryptionService(encryptionKey));

        // The secrets a deployed host must have chosen itself. Gathered from the keys they already live
        // under and judged on start: a value copied from a template (or the developer fallback of an
        // unset Jwt:Secret) is public, so the host refuses to boot on it instead of looking configured.
        services.AddSingleton<IValidateOptions<DeployedSecrets>, DeployedSecretsValidator>();
        services.AddOptions<DeployedSecrets>()
            .Configure(options =>
            {
                options.JwtSecret = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.Secret)}"]
                    ?? JwtTokenService.DevSecretFallback;
                options.EncryptionKey = encryptionKey;
                options.BackchannelLogoutKey = configuration[
                    $"{BackchannelLogoutOptions.SectionName}:{nameof(BackchannelLogoutOptions.Key)}"];
                options.DatabasePassword = DatabasePasswordOf(configuration.GetConnectionString("DefaultConnection"));
            })
            .ValidateOnStart();

        // Secret settings (gateway keys, registrar passwords, the setup token) are encrypted with
        // the same key.
        services.AddSingleton<ISecretSettingCipher, SecretSettingCipher>();

        // Outbound HTTP resilience.
        //
        // Registered first because every AddHttpClient below reaches for it. Until this existed
        // not one of the eleven client registrations in this file had a retry, a breaker or a
        // timeout of its own, so a registrar or a control panel that stopped answering held a
        // request thread for HttpClient's 100-second default, and a third party having a bad
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
        // Domain events are delivered by an interceptor on the context, per scope: the gate lets a
        // bulk writer (migration pull, dev seed) discard events, and the interceptor reads it.
        services.AddScoped<IDomainEventGate, DomainEventGate>();
        services.AddScoped<DomainEventDispatchInterceptor>();

        // Creation and change timestamps are stamped on save from the one clock, never by a factory.
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            options.AddInterceptors(
                serviceProvider.GetRequiredService<AuditableEntityInterceptor>(),
                serviceProvider.GetRequiredService<DomainEventDispatchInterceptor>());

            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                    // One SQL statement per collection navigation rather than one join
                    // over all of them. Domain reads include four collections, and the
                    // single-query default multiplies their rows together. A query that needs the single statement's snapshot
                    // consistency opts back in with AsSingleQuery(); none does today.
                    npgsql.UseQuerySplittingBehavior(
                        Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery);
                });
        });

        // The unit of work handlers write against and save through: the same scoped context
        // instance, seen through the Application-layer port so no handler references Infrastructure.
        services.AddScoped<IHostpanelDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IClientRecordStore, ClientRecordStore>();
        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<IClientCredit, ClientCredit>();
        services.AddScoped<ISetupTokenStore, SetupTokenStore>();
        services.AddScoped<INumberSequence, NumberSequence>();

        // Which instance runs a recurring job: the shared lease the job handlers take before a pass.
        services.AddScoped<IJobLease, JobLease>();

        // Storefront branding uploads. Both ports are stateless, so they are registered as
        // singletons: the processor holds only the ceilings it was configured with, and the
        // storage resolves the web root per call.
        services.AddOptions<BrandingOptions>()
            .Bind(configuration.GetSection(
                BrandingOptions.SectionName))
            .Validate(
                o => o.MaxBytes > 0 && o.MaxSourceEdge > 0 && o.MaxLogoEdge > 0,
                "Branding ceilings must all be positive.")
            .Validate(
                o => !Path.IsPathRooted(o.RelativePath) && !o.RelativePath.Contains(".."),
                "Branding:RelativePath must stay inside the web root.")
            .ValidateOnStart();
        services.AddSingleton<IBrandingImageProcessor,
            BrandingImageProcessor>();
        services.AddSingleton<IBrandingStorage,
            FileSystemBrandingStorage>();

        // Invoice PDFs: stateless, and the fonts it embeds are loaded once per process.
        services.AddSingleton<IValidateOptions<InvoicePdfOptions>, InvoicePdfOptionsValidator>();
        services.AddOptions<InvoicePdfOptions>()
            .Bind(configuration.GetSection(InvoicePdfOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<InvoicePdfRenderGate>();
        services.AddSingleton<IInvoicePdfRenderer, MigraDocInvoicePdfRenderer>();

        // Where people live decides almost everything below. Read from configuration once,
        // here, so there is one answer rather than a scattering of mode checks that can disagree.
        var ownsItsUsers = AuthMode.IsLocal(
            configuration.GetSection(AuthModeOptions.SectionName).Get<AuthModeOptions>()?.Mode);
        var ssoOptions = configuration.GetSection(SsoOptions.SectionName).Get<SsoOptions>() ?? new SsoOptions();

        // The one DI-resolvable answer to the same question, for everything downstream
        // of service registration.
        services.AddSingleton<IAuthModeProvider>(new ConfigurationAuthModeProvider(ownsItsUsers));

        // The sign-out fan-out's key: required under sso, optional in local mode (an unset key
        // refuses every call). The validator asks the mode provider registered just above.
        services.AddSingleton<IValidateOptions<BackchannelLogoutOptions>, BackchannelLogoutOptionsValidator>();
        services.AddOptions<BackchannelLogoutOptions>()
            .Bind(configuration.GetSection(BackchannelLogoutOptions.SectionName))
            .ValidateOnStart();

        // The signer's settings; the composition root reads the same class for its validators.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        // Session lifetimes: access-token length and the absolute maximum age of a renewed session.
        services.AddSingleton<IValidateOptions<SessionOptions>, SessionOptionsValidator>();
        services.AddOptions<SessionOptions>()
            .Bind(configuration.GetSection(SessionOptions.SectionName))
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        // AppName is a bare top-level key, not a section; unset keeps the class default.
        var appName = configuration[LocalTwoFactorOptions.ConfigurationKey];
        services.AddOptions<LocalTwoFactorOptions>()
            .Configure(o =>
            {
                if (!string.IsNullOrWhiteSpace(appName))
                {
                    o.Issuer = appName;
                }
            })
            .ValidateOnStart();

        // The local token signer, registered in both modes on purpose: the admin SPA signs in
        // with a local JWT whatever owns the accounts, so the port must resolve even where an
        // the sign-on provider issues the client portal's tokens. Singleton: it holds no per-request state.
        services.AddSingleton<IJwtService, JwtTokenService>();

        if (ownsItsUsers)
        {
            // ASP.NET Core Identity: use AddIdentityCore to avoid overriding the JWT Bearer
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

            // Wrong passwords lock the account for a while. The count lives on the account row
            // (AccessFailedCount, LockoutEnd), so it holds across instances and restarts.
            // The development seed's passwords. Validated when the seeder asks for them, not on start:
            // only appsettings.Development.json names them, and no other tier runs the seeder.
            services.AddOptions<DevDataSeederOptions>()
                .Bind(configuration.GetSection(DevDataSeederOptions.SectionName))
                .ValidateDataAnnotations();

            services.AddOptions<SignInLockoutOptions>()
                .Bind(configuration.GetSection(SignInLockoutOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddOptions<IdentityOptions>()
                .Configure<IOptions<SignInLockoutOptions>>((identity, lockout) =>
                {
                    identity.Lockout.AllowedForNewUsers = true;
                    identity.Lockout.MaxFailedAccessAttempts = lockout.Value.MaxFailedAttempts;
                    identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockout.Value.LockoutMinutes);
                });

            // The pending token between the password step and the one-time code: signed, encrypted
            // with the Data Protection key ring, short-lived, spendable once, attempt-limited.
            // Local mode only, because that is the only mode that signs anyone in with a password.
            //
            // The key ring lives in the database (DataProtectionKeys), not in the container: a ring
            // kept on the container's disk is lost on every redeploy and differs per instance, so a
            // challenge issued by one instance, or before a deploy, could not be read by the next,
            // and neither could Identity's email confirmation and password reset tokens. One
            // application name on every instance makes them derive the same keys from that ring.
            // The key XML is stored as the framework writes it, not encrypted again: it is guarded
            // by the database's access control, the same boundary as every row it protects.
            // (Single sign-on mode is left alone: the sign-on library keeps its own shared ring.)
            services.AddDataProtection()
                .SetApplicationName(DataProtectionApplicationName)
                .PersistKeysToDbContext<AppDbContext>();
            services.AddOptions<TwoFactorChallengeOptions>()
                .Bind(configuration.GetSection(TwoFactorChallengeOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<ITwoFactorChallengeService, TwoFactorChallengeService>();
            services.AddOptions<TwoFactorCodeGuardOptions>()
                .Bind(configuration.GetSection(TwoFactorCodeGuardOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<ITwoFactorCodeGuard, TwoFactorCodeGuard>();
        }
        else
        {
            // No local password step and no local code exist in this mode, but the local sign-in
            // handlers still do, and their code is generated at image build time in the image's mode
            // with service location refused. These stand-ins open and accept nothing, so the container
            // is complete in every mode without registering Identity or its key ring.
            services.AddSingleton<ITwoFactorChallengeService, SsoTwoFactorChallengeService>();
            services.AddSingleton<ITwoFactorCodeGuard, SsoTwoFactorCodeGuard>();
        }

        // Auth services
        // Roles live in this product's own database in both modes, keyed by whatever the
        // configured identity provider calls a person. Registered outside the Identity
        // block above because it does not depend on Identity being registered at all:
        // which is the point, since single sign-on mode will stop registering it.
        services.AddScoped<ISubjectRoleStore, SubjectRoleStore>();

        // The client portal keeps its sign-in sessions in Redis; a back-channel logout ends them too.
        // In both modes, beside the mode's own revoker: the handler asks every registered one. With
        // no Redis configured the portal keeps its sessions in its own memory and there is nothing to add.
        services.AddOptions<PortalSessionOptions>()
            .Bind(configuration.GetSection(PortalSessionOptions.SectionName))
            .ValidateOnStart();
        if (!string.IsNullOrWhiteSpace(configuration[$"{PortalSessionOptions.SectionName}:{nameof(PortalSessionOptions.RedisConnection)}"]))
        {
            services.AddSingleton<ISessionRevoker, PortalSessionRevoker>();
        }

        // One provider, never both. In single sign-on mode ASP.NET Identity is not registered at all,
        // so there is no code path that could write a person row even by accident, which
        // is what let a second, drifting copy of every user exist before.
        if (ownsItsUsers)
        {
            services.AddScoped<IIdentityProvider, LocalIdentityProvider>();
            services.AddScoped<IUserProvisioning, LocalUserProvisioning>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITwoFactorService, LocalTwoFactorService>();
            services.AddScoped<ISessionRevoker, LocalSessionRevoker>();
        }
        else
        {
            services.AddScoped<IIdentityProvider, SsoIdentityProvider>();
            services.AddScoped<IUserProvisioning, SsoModeUserProvisioning>();
            services.AddScoped<ITwoFactorService, SsoTwoFactorService>();

            // An account store with no accounts in it, for the local sign-in and registration
            // handlers whose routes answer 404 here (LocalModeOnlyFilter); see SsoUserService.
            services.AddScoped<IUserService, SsoUserService>();
#if INNOVAYSE_SSO
            // Sessions live in the shared Innovayse.Auth store, so revoking is that store's call.
            services.AddScoped<ISessionRevoker, SsoSessionRevoker>();
#else
            // A standalone build cannot run in sso mode (the API refuses to boot), so this is
            // never resolved; it exists so the container is complete in every build.
            services.AddScoped<ISessionRevoker, LocalSessionRevoker>();
#endif

            // The sign-on provider's service API, addressed by the same authority the token validation
            // uses. The service key is this product's own credential, not a user's.
            services.AddHttpClient<SsoServiceClient>(client =>
            {
                var authority = ssoOptions.Authority
                    ?? throw new InvalidOperationException(
                        "Sso:Authority must be set when Auth:Mode is not 'local', "
                        + "it is where this product reads its people from.");
                client.BaseAddress = new Uri(authority.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("X-Service-Key", ssoOptions.ServiceKey ?? string.Empty);
            })
            // Every operation on this client is a GET that reads a person, so all of them are
            // safe to repeat. The breaker's shape, and the fact that an open one means nobody
            // is looked up, is argued on HttpResilienceOptions.SsoRead.
            .AddReadOnlyResilience(o => o.SsoRead);

            // The sign-on provider's TOTP endpoints, addressed the same way, but unlike SsoServiceClient
            // above, no X-Service-Key: two-factor acts as the calling person, not as the
            // platform, so each call carries the caller's own bearer token instead (see
            // SsoTwoFactorService).
            services.AddHttpClient<SsoTwoFactorClient>(client =>
            {
                var authority = ssoOptions.Authority
                    ?? throw new InvalidOperationException(
                        "Sso:Authority must be set when Auth:Mode is not 'local', "
                        + "it is where this product reads its people from.");
                client.BaseAddress = new Uri(authority.TrimEnd('/') + "/");
            })
            // No retry stage. All three TOTP operations are POSTs that change a person's second
            // factor: enable issues a fresh secret, so a repeat hands back a different QR code
            // from the one already on screen, and verify and disable each spend a one-time code.
            .AddNoRetryResilience(o => o.SsoTwoFactor);
        }

        // Domain ownership rule for the client-facing routes. The client-facing My* handlers
        // call it themselves, so the check travels with the message: every command behind
        // those endpoints is also dispatched by the admin DomainsController, and
        // RenewDomainCommand by the auto-renew job as well.
        services.AddScoped<IDomainOwnership, DomainOwnership>();

        // The same rule for support tickets and for invoices, in the same shape.
        services.AddScoped<ITicketOwnership, TicketOwnership>();
        services.AddScoped<IInvoiceOwnership, InvoiceOwnership>();

        // The same rule for client services. The client-facing service handlers call it themselves.
        services.AddScoped<IServiceOwnership, ServiceOwnership>();

        // What the signed-in user may do on a client account. The portal's permission filter decides
        // with it and the account endpoint reports it; both read it per request and neither trusts
        // anything the request says about it.
        services.AddScoped<IClientAccess, ClientAccess>();
        services.AddScoped<ICallerClientResolver, CallerClientResolver>();

        // Service provisioning settings, bound and checked at startup: either the whole section
        // is unset, a deployment that provisions through per-server credentials held in the
        // database instead, or it is complete. A half-filled section would bind silently and fail on the first call.
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
            // section is an error here even though it is allowed at startup: said plainly and
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
            httpClient.Timeout = HttpBackstopTimeouts.CPanel;
            httpClient.DefaultRequestHeaders.Add(
                HeaderNames.Authorization,
                $"WHM {settings.Username}:{settings.ApiToken}");
        })
            // No retry stage, and the HTTP method is no guide: every function this client calls
            // goes out as one POST whose function name is in the path, and createacct, removeacct
            // and passwd are writes among the reads (accountsummary). A method-based predicate
            // would repeat all of them.
            .AddNoRetryResilience(o => o.CPanel);
        // Use NullCPanelProvisioningProvider as fallback for unmigrated code
        services.AddScoped<IProvisioningProvider, NullCPanelProvisioningProvider>();

        // Provisioning behaviour switches. Left unset everywhere but development, where
        // appsettings.Development.json turns UseFakeProvider on so the setup flow can complete
        // without a live CWP7 panel to call.
        services.AddOptions<ProvisioningOptions>()
            .Bind(configuration.GetSection(
                ProvisioningOptions.SectionName))
            .ValidateOnStart();

        // Provisioning provider factory: creates per-server providers (CWP7, cPanel, etc.)
        services.AddScoped<IProvisioningProviderFactory, ProvisioningProviderFactory>();

        // SSH key management: per-server providers resolved from Server.Module plus the server's
        // own ServerSshSettings row. Nothing here names a server; every value is per row.
        services.AddSingleton<SshAgentTransport>();
        services.AddScoped<ISshKeyProviderFactory, SshKeyProviderFactory>();
        services.AddScoped<ISshServiceContextResolver, SshServiceContextResolver>();
        services.AddScoped<IHostedAccountOperations, HostedAccountOperations>();
        services.AddScoped<ISshKeyRevoker, SshKeyRevoker>();

        // Connecting a server: the key pair HostPanel signs agent calls with, the host key pinned on
        // first contact, and the agent files the admin panel hands out for installation.
        services.AddSingleton<ISshIdentityGenerator, SshIdentityGenerator>();
        services.AddSingleton<ISshHostKeyScanner, SshHostKeyScanner>();
        services.AddSingleton<ISshAgentBundle, EmbeddedSshAgentBundle>();

        // Billing services
        services.AddScoped<IPayerCurrencyResolver, PayerCurrencyResolver>();

        // Exchange rates come from the Central Bank of Armenia's bulletin. The URL and its shape
        // are argued on CbaExchangeRateSource; the read-only profile with retries is argued on
        // HttpResilienceOptions.Cba. The interface is what the handler depends on, so another
        // bank is another class here and nothing in Application moves.
        services.AddHttpClient<IExchangeRateSource, CbaExchangeRateSource>(client =>
        {
            client.BaseAddress = new Uri(CbaExchangeRateSource.BaseAddress);
        })
            .AddReadOnlyResilience(o => o.Cba);

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
        services.AddScoped<ICardPaymentGateway, StripeService>();

        // Payment plugins (hosted-gateway providers)
        services.AddHttpClient();

        // The factory's unnamed client: what CreateClient() with no argument hands back. It is
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
        // no registration, and IHttpClientFactory answers an unknown name with a default client
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
        // endpoint out of the path: a repeated refund.do is a second refund. The timeout, and
        // why this money-moving client gets a breaker when the others do not, are argued on
        // HttpResilienceOptions.Inecobank.
        services.AddHttpClient(HttpClientResilienceExtensions.InecobankClientName, client =>
        {
            // Outer backstop only. The pipeline below decides in 15s and 40s; this catches a
            // pipeline that was configured wrong, nothing else.
            client.Timeout = HttpBackstopTimeouts.Inecobank;
        })
            .AddInecobankResilience();

        services.AddScoped<IPaymentPluginResolver, PaymentPluginResolver>();

        // The chat notifier, the operator's channel for public contact-form enquiries.
        // Optional in the same way the card gateway and the default registrar are: an unset section is a deployment that
        // runs no bot, and the notifier logs that it did nothing rather than failing an enquiry
        // the mail already delivered. A half-filled section is refused here instead, because a
        // token with no chat id posts nowhere while looking configured.
        //
        // The token is a path segment of every Bot API call rather than a header, so it cannot be
        // baked into the base address at registration; the notifier reads it from options per
        // call. The base address is the API root and ends in a slash: without it, the relative
        // "bot<token>/sendMessage" would replace the last segment instead of extending the path.
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .Validate(
                o => Uri.TryCreate(o.ApiUrl, UriKind.Absolute, out _) && o.ApiUrl.EndsWith('/'),
                $"{TelegramOptions.SectionName}:{nameof(TelegramOptions.ApiUrl)} must be an absolute URL ending in a slash.")
            .Validate(
                o => o.IsUsable,
                $"{TelegramOptions.SectionName} is partly configured: BotToken and ChatId must "
                    + "either both be set or the whole section left unset.")
            .ValidateOnStart();
        services.AddHttpClient<IContactNotifier, TelegramContactNotifier>((sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<TelegramOptions>>().Value.ApiUrl);

            // Short on purpose. This call sits between a delivered enquiry and the visitor's
            // answer, so a Telegram that is merely slow must not hold the response open. Now the
            // outer backstop for a pipeline that decides in 8s and 9s.
            client.Timeout = HttpBackstopTimeouts.Telegram;
        })
            // No retry stage. sendMessage is a POST with no idempotency key, so a repeat after a
            // lost response posts the enquiry to the operator's chat twice, and the mail has
            // already delivered it, so the duplicate buys nothing.
            .AddNoRetryResilience(o => o.Telegram);

        // Reports
        services.AddScoped<ISslMonitoringService, SslMonitoringService>();
        services.AddScoped<IHostingProbe, HostingProbe>();
        services.AddScoped<IDiskUsageService, DiskUsageService>();

        // Notifications
        // The Smtp section as a whole is deliberately not checked for completeness here, unlike
        // the integration options above. A deployment may fill it from several configuration
        // sources, none of them complete on its own, and may leave the Notifications section out
        // entirely. Refusing a partly filled section would refuse to start such a deployment, so a
        // missing host or port surfaces where the mail is actually sent instead.
        //
        // Encryption is the one value that cannot wait for that, which is why this registration
        // now validates at all. It replaced a UseSsl bool that was allowed to be absent only
        // because the port silently decided in its place; with the guess gone, an unset enum
        // would take its zero value, None, and hand the relay's password to the network in
        // the clear on a host that looks healthy. Absent is refused here, by name. A value that
        // is not tls/ssl/none never reaches this check: the binder fails on it first, and
        // ValidateOnStart is what drags that failure forward from the first mail send to boot.
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(
                o => o.HasEncryption,
                $"{SmtpOptions.SectionName}:{nameof(SmtpOptions.Encryption)} is required and must "
                    + "be one of tls (STARTTLS, conventionally port 587), ssl (implicit TLS, "
                    + "conventionally port 465) or none (a local mail catcher). Set "
                    + "SMTP_ENCRYPTION in this deployment's .env.")
            .ValidateOnStart();
        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<TemplateRenderer>();
        // The brand every mail is rendered with, read from the portal.* settings on each render
        // so an operator's change in the admin panel reaches the next mail without a restart.
        services.AddScoped<IEmailBrandingProvider, SettingsEmailBrandingProvider>();

        // Outbound address guard. Every client below that dials a host an admin typed (a
        // provisioning server, a registrar API, an SSH key agent) connects through OutboundConnector,
        // which resolves the name itself and never dials loopback, link-local or a metadata endpoint.
        // Private ranges are allowed by default (OutboundNetwork:AllowPrivateNetworks), because
        // panels and their servers usually share a private network with this one.
        services.AddOptions<OutboundNetworkOptions>()
            .Bind(configuration.GetSection(OutboundNetworkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Private routes to panels whose API port is closed to the internet (a VPN address per
        // host name); OutboundConnector dials a listed host at its route, see PanelNetworkOptions.
        services.AddOptions<PanelNetworkOptions>()
            .Bind(configuration.GetSection(PanelNetworkOptions.SectionName))
            .Validate(
                o => o.IsValid,
                $"{PanelNetworkOptions.SectionName}:{nameof(PanelNetworkOptions.HostAddresses)} must be comma-separated "
                    + "hostname=address pairs, e.g. srv4.example.com=10.77.0.1.")
            .ValidateOnStart();
        services.AddSingleton<OutboundConnector>();

        // Servers: connection testing and placement of new accounts.
        services.AddScoped<IServerConnectionTester, ServerConnectionTester>();
        services.AddScoped<IServerSelector, ServerSelector>();

        // CWP API client.
        //
        // No retry stage and no breaker. Every CWP call is a form POST and the operation lives in
        // the body's `action` field, not in the method or the path: create, suspend, unsuspend and
        // terminate all post to /v1/account, and so does the account listing behind the
        // server-info screen. A predicate cannot separate the read from the writes without
        // re-reading a consumed request body, and guessing wrong re-creates a hosting account.
        // The breaker is off because the server is a per-call argument rather than a base
        // address, so one dead node would open it for every healthy one.
        services.AddHttpClient<IHostingPanelApiClient, CwpApiClient>(client =>
        {
            client.Timeout = HttpBackstopTimeouts.Cwp;
        })
            .AddOutboundAddressGuard()
            .AddNoRetryResilience(o => o.Cwp);
        // Named twin of the typed registration above, for the same reason the CWP7 pair exists:
        // CwpProvisioningProvider builds its own CwpApiClient (the interface exposes only the
        // server-info call) and resolves this client by name. That keeps the
        // provisioning path, the one that matters most, inside the timeout and the no-retry pipeline.
        services.AddHttpClient(CwpApiClient.HttpClientName, client =>
        {
            client.Timeout = HttpBackstopTimeouts.Cwp;
        })
            .AddOutboundAddressGuard()
            .AddNoRetryResilience(o => o.Cwp);

        // CWP7 API client (named + typed so the factory can also resolve it by name).
        // Both registrations carry the same profile for the same two reasons as CWP above; the
        // named one is what ProvisioningProviderFactory resolves per server, so it is what
        // covers the provisioning path, the one that matters most.
        services.AddHttpClient(HttpClientNames.Cwp7, client =>
        {
            client.Timeout = HttpBackstopTimeouts.Cwp7;
        })
            .AddOutboundAddressGuard()
            .AddNoRetryResilience(o => o.Cwp7);
        services.AddHttpClient<IHostingPanelV7ApiClient, Cwp7ApiClient>(client =>
        {
            client.Timeout = HttpBackstopTimeouts.Cwp7;
        })
            .AddOutboundAddressGuard()
            .AddNoRetryResilience(o => o.Cwp7);

        // Migration
        services.AddScoped<MigrationPullWorker>();
        services.AddScoped<MigrationPullQueue>();
        services.AddScoped<MigrationRecordPager>();
        services.AddScoped<MigrationClientResolver>();
        services.AddScoped<MigrationRecordRunner>();
        services.AddScoped<MigrationProductLookup>();
        services.AddScoped<IMigrationEntityImporter, ClientsImporter>();
        services.AddScoped<IMigrationEntityImporter, ProductsImporter>();
        services.AddScoped<IMigrationEntityImporter, InvoicesImporter>();
        services.AddScoped<IMigrationEntityImporter, ServicesImporter>();
        services.AddScoped<IMigrationEntityImporter, DomainsImporter>();
        services.AddScoped<IMigrationEntityImporter, TicketsImporter>();
        services.AddScoped<IMigrationEntityImporter, OrdersImporter>();
        services.AddScoped<IMigrationEntityImporter, TransactionsImporter>();
        services.AddScoped<IMigrationEntityImporter, QuotesImporter>();
        services.AddScoped<IMigrationEntityImporter, KnowledgebaseImporter>();
        services.AddScoped<IMigrationEntityImporter, ContactsImporter>();
        services.AddScoped<IMigrationEntityImporter, TicketRepliesImporter>();
        services.AddScoped<IMigrationEntityImporter, AnnouncementsImporter>();
        services.AddScoped<IMigrationEntityImporter, DownloadsImporter>();
        services.AddScoped<IMigrationEntityImporter, NetworkIssuesImporter>();
        services.AddScoped<IMigrationSource, MigrationSourceClient>();
        services.AddSingleton<IMigrationPluginArchive, MigrationPluginArchive>();
        // The folder may be missing on a deployment that does not ship the plugin; the download then
        // answers MIGRATION_PLUGIN_NOT_FOUND. Only a blank path is a configuration error.
        services.AddOptions<MigrationPluginOptions>()
            .Bind(configuration.GetSection(MigrationPluginOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Path),
                $"{MigrationPluginOptions.SectionName}:{nameof(MigrationPluginOptions.Path)} must not be blank "
                    + $"(set {MigrationPluginOptions.SectionName}__{nameof(MigrationPluginOptions.Path)}).")
            .ValidateOnStart();
        // The one client where the POST is misleading in the safe direction: the pull protocol
        // posts a signed payload and answers with data, so ping, totals and a page of records all
        // read and nothing on the far side is created or spent. Retrying everything is therefore
        // correct here. No breaker: the source URL is a per-job argument, so one bad install
        // would fail jobs against every other.
        //
        // The source URL is admin input this server calls: the primary handler dials through the shared
        // OutboundConnector (the one table of refused ranges), with this source's own, stricter private
        // network setting (off unless MigrationSource:AllowPrivateNetworks), follows no redirect, and every body is
        // capped at MaxResponseBytes (MigrationSourceClient reads it into a buffer of that size).
        services.AddOptions<MigrationSourceOptions>()
            .Bind(configuration.GetSection(MigrationSourceOptions.SectionName))
            .Validate(o => o.MaxResponseBytes > 0, "MigrationSource:MaxResponseBytes must be positive.")
            .ValidateOnStart();
        services.AddHttpClient(HttpClientNames.Migration, (sp, client) =>
        {
            client.Timeout = HttpBackstopTimeouts.Migration;
            client.MaxResponseContentBufferSize = sp.GetRequiredService<IOptions<MigrationSourceOptions>>().Value.MaxResponseBytes;
        })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                // A migration source is not a control panel, so the panel routes do not apply to it.
                var connector = new OutboundConnector(
                    Options.Create(new OutboundNetworkOptions
                    {
                        AllowPrivateNetworks = sp.GetRequiredService<IOptions<MigrationSourceOptions>>().Value.AllowPrivateNetworks,
                        ConnectTimeoutSeconds = sp.GetRequiredService<IOptions<OutboundNetworkOptions>>().Value.ConnectTimeoutSeconds,
                    }),
                    Options.Create(new PanelNetworkOptions()));
                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectTimeout = connector.ConnectTimeout,
                    ConnectCallback = connector.ConnectAsync,
                };
            })
            .AddReadOnlyResilience(o => o.Migration);

        // Domains
        services.AddScoped<IRegistrarProvider, NameAmRegistrarProvider>();
        services.AddScoped<IRegistrarProviderFactory, RegistrarProviderFactory>();
        services.AddScoped<RegistrarProviderResolver>();
        services.AddScoped<RegistrantContactReader>();
        services.AddScoped<RenewalSettingsReader>();
        services.AddScoped<NameAmRegistrarProvider>();
        services.AddScoped<IIntegrationConnectionProbe, NameAmConnectionProbe>();
        services.AddScoped<NamecheapRegistrarProvider>();
        // One access token per Name.am connection for the whole process: the client itself is
        // created per request and could not keep it.
        services.AddSingleton<NameAmTokenStore>();
        // Reads and purchases on the same client, so the predicate has to tell them apart rather
        // than the client being retried wholesale. GET and PUT are repeated, Name.am's PUT is a
        // whole-resource update of nameservers, contacts or the lock, and POST only for the
        // availability check and the login, never for /client/carts/purchase, which registers,
        // transfers or renews a domain and bills for it.
        services.AddHttpClient<NameAmClient>(client =>
        {
            client.Timeout = HttpBackstopTimeouts.Registrar;
        })
            .AddOutboundAddressGuard()
            .AddNameAmResilience();
        services.AddOptions<NameAmOptions>()
            .Bind(configuration.GetSection(NameAmOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{NameAmOptions.SectionName} is partly configured: Email and Password must either "
                    + "both be set or the whole section left unset.")
            .ValidateOnStart();

        // Every Namecheap call is a GET to the same URL, so the method says nothing and the
        // operation is the Command query parameter, which ranges from namecheap.domains.check
        // to namecheap.domains.create on the same verb. The predicate reads that parameter and
        // repeats only the lookups; anything it does not recognise counts as a write.
        services.AddHttpClient<NamecheapClient>(client =>
        {
            client.Timeout = HttpBackstopTimeouts.Registrar;
        })
            .AddNamecheapResilience();
        services.AddOptions<NamecheapOptions>()
            .Bind(configuration.GetSection(NamecheapOptions.SectionName))
            .Validate(
                o => o.IsUsable,
                $"{NamecheapOptions.SectionName} is partly configured: ApiUser, ApiKey and ApiUrl "
                    + "must either all be set or the whole section left unset.")
            .ValidateOnStart();

        // Plugins: the build's own folder first (its plugins cannot be removed or shadowed), then
        // the upload folder. The options are read here as well as bound, because the loader runs
        // while the container is still being built.
        services.AddSingleton<IValidateOptions<PluginOptions>, PluginOptionsValidator>();
        services.AddOptions<PluginOptions>()
            .Bind(configuration.GetSection(PluginOptions.SectionName))
            .ValidateOnStart();
        services.AddScoped<IPluginPackageStore, PluginPackageStore>();
        var pluginOptions = configuration.GetSection(PluginOptions.SectionName).Get<PluginOptions>() ?? new PluginOptions();
        PluginLoader.DiscoverAndRegister(services, [PluginOptions.BuiltInRoot, pluginOptions.InstallRoot], loggerFactory);

        // Every plugin is turned into an instance by the one activator (its settings gate and configuration),
        // whichever resolver or runner asks for it. Scoped: it reads the settings through the request's context.
        services.AddScoped<PluginActivator>();
        services.AddScoped<IFulfilmentPluginResolver, FulfilmentPluginResolver>();
        services.AddScoped<IIdentityPluginResolver, IdentityPluginResolver>();

        // Hosted plugins start once the host is up and stop as it begins to stop, each failure kept to its plugin.
        // Registered in every environment: with no hosted plugin loaded it opens nothing and reads nothing.
        services.AddHostedService<HostedPluginRunner>();

        // Public demo. Last on purpose: when Demo:Enabled is true this swaps the outward ports
        // registered above (payment, registrar, provisioning, rates, monitoring) for offline fakes,
        // and a Replace can only replace what is already there. Email is not among them: it stays
        // on the configured relay so a demo can show real mail in a catcher.
        var demo = configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();
        services.AddDemoMode(demo, ownsItsUsers);

        return services;
    }

    /// <summary>Reads the password out of a PostgreSQL connection string.</summary>
    /// <param name="connectionString">The configured connection string; may be unset.</param>
    /// <returns>The password, or null when the string is unset, malformed (the database registration reports that) or holds none.</returns>
    private static string? DatabasePasswordOf(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Password;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
