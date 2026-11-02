using System.Text.Json.Serialization;
using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.API.Extensions;
using Innovayse.Hostpanel.API.Identity;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Scheduling;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Filters;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using CorsPolicyOptions = Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace Innovayse.Hostpanel.API;

/// <summary>
/// Registers what the web edge owns: CORS and the redirect-safe origins list, the client
/// portal and compatibility options, the request-scoped identity adapter, MVC and the Swagger document, the
/// action-timing filter, the ProblemDetails surfaces that answer before a controller runs, the
/// domain and billing cron jobs, and the edge rate limiter.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Adds the API layer.</summary>
    /// <param name="services">The container.</param>
    /// <param name="configuration">Host configuration, read here and nowhere below.</param>
    /// <param name="environment">The host environment, needed by the rate limiter's Testing-environment bypass.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // One Cors section feeds both the CORS policy and the gateway return URL check, so a payer
        // may only be handed back to an origin the web edge already trusts. Blank entries are
        // optional slots a deployment left unset and are skipped, never a startup failure.
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .Validate(
                o => o.IsValid,
                $"{CorsOptions.SectionName}:{nameof(CorsOptions.AllowedOrigins)} "
                    + "must contain absolute origins only, e.g. https://host.example.com.")
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsPolicyOptions>()
            .Configure<IOptions<CorsOptions>>((cors, origins) =>
                cors.AddDefaultPolicy(policy =>
                    policy.WithOrigins(origins.Value.Origins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials()));

        // ClientBaseUrl and DefaultLocale are bare top-level keys, never sections, so they are read
        // by key here and left at the option class's default when the deployment sets neither.
        var clientBaseUrl = configuration[ClientPortalOptions.ConfigurationKey];
        services.AddOptions<ClientPortalOptions>()
            .Configure(o =>
            {
                if (!string.IsNullOrWhiteSpace(clientBaseUrl))
                {
                    o.BaseUrl = clientBaseUrl;
                }
            })
            .Validate(
                o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
                $"{ClientPortalOptions.ConfigurationKey} must be an absolute URL including the scheme, "
                    + "e.g. https://client.example.com.")
            .ValidateOnStart();

        // AdminBaseUrl is the same kind of key: where staff land from a mailed link.
        var adminBaseUrl = configuration[AdminPanelOptions.ConfigurationKey];
        services.AddOptions<AdminPanelOptions>()
            .Configure(o =>
            {
                if (!string.IsNullOrWhiteSpace(adminBaseUrl))
                {
                    o.BaseUrl = adminBaseUrl;
                }
            })
            .Validate(
                o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
                $"{AdminPanelOptions.ConfigurationKey} must be an absolute URL including the scheme, "
                    + "e.g. https://admin.example.com.")
            .ValidateOnStart();

        // Where agent connections come from, for the firewall and sshd lines the admin panel shows.
        services.AddOptions<SshAgentOptions>()
            .Bind(configuration.GetSection(SshAgentOptions.SectionName))
            .Validate(
                o => o.IsValid,
                $"{SshAgentOptions.SectionName}:SourceAddresses must be comma-separated IPv4 or IPv6 addresses, e.g. 203.0.113.10.")
            .ValidateOnStart();

        // Contact addresses for the support departments seeded on a fresh install.
        services.AddOptions<DefaultDepartmentsOptions>()
            .Bind(configuration.GetSection(DefaultDepartmentsOptions.SectionName))
            .Validate(
                o => o.IsValid,
                $"{DefaultDepartmentsOptions.SectionName} addresses must each be a mailbox such as support@example.com.")
            .ValidateOnStart();

        // Order numbering and the payment term of an order's invoice.
        services.AddOptions<OrderOptions>()
            .Bind(configuration.GetSection(OrderOptions.SectionName))
            .Validate(
                o => o.IsValid,
                $"{OrderOptions.SectionName} needs at least one number digit, a prefix and digits that fit an order "
                    + "number, and an invoice payment term of 0 to 365 days.")
            .ValidateOnStart();

        // How large a request body the host passes on to a plugin route.
        services.AddOptions<PluginApiOptions>()
            .Bind(configuration.GetSection(PluginApiOptions.SectionName))
            .Validate(
                o => o.IsValid,
                $"{PluginApiOptions.SectionName}:{nameof(PluginApiOptions.MaxBodyBytes)} must be from 1 to "
                    + $"{PluginApiOptions.MaxBodyBytesCeiling} bytes.")
            .ValidateOnStart();

        // The caller of the request, for handlers. Registered by type, not through a factory, so
        // Wolverine's generated handler code builds it as a constructor call like any other adapter.
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
        services.AddScoped<ICallerClientMemo, HttpCallerClientMemo>();

        // The caller's own bearer token, for an adapter that acts as the person against an outside service.
        services.AddScoped<ICallerAccessToken, HttpCallerAccessToken>();

        // MVC and the generated OpenAPI document
        services.AddMemoryCache();
        services.AddControllers(opts =>
            {
                // Every 201 Created in this API is built with CreatedAtAction and nameof(GetAsync).
                // MVC trims the "Async" suffix from action names by default, so the name would never
                // match and the framework would throw "No route matches the supplied values" after the
                // row was written: the caller sees a 400 for a creation that worked, and a retry makes
                // a duplicate. Setting it here covers every controller at once. No action name is
                // referenced as a string anywhere, so nothing depends on the trimmed form.
                opts.SuppressAsyncSuffixInActionNames = false;

                // Every client portal action can answer 503 IDENTITY_PLUGIN_UNAVAILABLE; declared once here.
                opts.Conventions.Add(new IdentityPluginUnavailableConvention());

                // Required-ness is the validators' business, where every message is localized. Without this MVC
                // makes a non-nullable string an implicit [Required] with a fixed English message, so a body
                // that leaves out a route-supplied id (`command with { MemberUserId = userId }`) is a 400
                // before the handler can overlay it.
                opts.ConfigureLocalizedModelBinding();
            })
            .AddJsonOptions(opts =>
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Three of the error surfaces that short-circuit before a controller or the exception
        // middleware runs: model binding, the bearer challenge/forbid, and an
        // authorization-policy refusal. Each writes the one ErrorResponse body instead of the
        // framework's ValidationProblemDetails or an empty 401/403.
        // BaseApiController carries [ServiceFilter(typeof(ActionTimingFilter))], so the filter and its
        // options are resolved per action and must be registered here; without them every action on a
        // BaseApiController controller fails before it runs.
        services.AddOptions<ActionTimingOptions>()
            .BindConfiguration(ActionTimingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Every 503 the error surfaces write carries Retry-After from these options.
        services.AddOptions<ServiceUnavailableOptions>()
            .BindConfiguration(ServiceUnavailableOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ActionTimingFilter>();

        // When every recurring job runs and how the instances share them; read by the startup services and the handlers.
        services.AddOptions<ScheduledJobsOptions>()
            .BindConfiguration(ScheduledJobsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Where release-update checks point and what upgrade command is shown. Operators that fork
        // the repository or run without outbound internet access override these without touching code.
        services.AddOptions<ReleaseOptions>()
            .BindConfiguration(ReleaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.ConfigureModelStateProblemDetails();
        services.ConfigureProblemDetailsAuthEvents();
        services.PostConfigure<JwtBearerOptions>(LocalJwtScheme.Name, o => o.UseProblemDetailsResponses());
        services.AddProblemDetailsAuthorizationResponses();
        // Replaces the decorator above: the same 401 and 403 bodies, plus 503 when only the identity plugin failed.
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationMiddlewareResultHandler, IdentityPluginUnavailableResponder>());
        services.AddHostpanelSwagger();

        // The two cron startups below each hand Wolverine a message to deliver hours from now. Wolverine
        // keeps that delay as a timer in the process-wide timer queue and does not cancel it when the
        // host stops, so in a process that starts and stops many hosts (the integration tests) every
        // stopped host stays reachable from the timer, with its whole service graph and the code
        // Wolverine compiled for it. The tests drive the crons by dispatching their messages directly
        // and never wait for the clock, so the Testing environment does not schedule them (issue 373).
        if (!environment.IsEnvironment(EnvironmentNames.Testing))
        {
            // Domain scheduled jobs: the expiry check, the TLD price sync and the pending status sync.
            // Their hours and intervals are the ScheduledJobsOptions values, not constants here.
            services.AddHostedService<DomainScheduledJobsStartup>();

            // Billing scheduled jobs: the daily billing, renewal and suspension crons and the gateway reconcile.
            services.AddHostedService<BillingScheduledJobsStartup>();

            // Admin scheduled jobs: the daily GitHub release-update check.
            services.AddHostedService<AdminScheduledJobsStartup>();

            // Recurring recovery of migration jobs left queued or in progress with a stale heartbeat. Not in Testing:
            // the tests derive extra hosts over one database mid-test, and a recovery there would
            // fail the pull another host of the same test is running.
            services.AddHostedService<MigrationRecoveryStartup>();
        }

        // Demo data reset: schedules the first run only when Demo:Enabled is true; a no-op otherwise.
        // Registered in every environment because the demo tests read the reset time it publishes.
        services.AddHostedService<DemoScheduledJobsStartup>();

        // Rate limiting, and the forwarded-header handling it depends on. Registered as one call
        // because both are the web edge's reading of the same proxy chain, and a limiter whose idea
        // of "the caller" disagrees with the rest of the pipeline's is worse than none: it would
        // partition every browser onto nginx's address and let one visitor's burst refuse everybody.
        //
        // Everything is limited by a global budget whether or not its action names a policy: see
        // RateLimitingExtensions for why an opt-in scheme does not survive fifty-eight controllers.
        services.AddPlatformRateLimiting(environment);

        return services;
    }
}
