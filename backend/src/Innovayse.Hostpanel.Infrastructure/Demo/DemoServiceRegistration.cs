using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Monitoring;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// Wires the public demo into the container. Always binds and validates <see cref="DemoOptions"/>
/// and registers the data manager and the reset schedule; only when <c>Demo:Enabled</c> is true does
/// it swap every outward port for its offline fake. The choice is made here, once, by configuration,
/// so no adapter or handler asks whether it is running in a demo.
/// </summary>
internal static class DemoServiceRegistration
{
    /// <summary>Registers the demo options, manager, schedule and, when enabled, the fake adapters.</summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="demo">The <c>Demo</c> section as the composition root read it, consulted only for
    /// the <see cref="DemoOptions.Enabled"/> switch that decides which adapters are registered. The
    /// validated options consumers receive are bound here from the host's configuration service.</param>
    /// <param name="ownsItsUsers">Whether accounts live in this product's own store (local mode).</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">The demo is enabled in a mode that has no local accounts.</exception>
    internal static IServiceCollection AddDemoMode(
        this IServiceCollection services, DemoOptions demo, bool ownsItsUsers)
    {
        services.AddSingleton<IValidateOptions<DemoOptions>, DemoOptionsValidator>();
        services.AddOptions<DemoOptions>()
            .BindConfiguration(DemoOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IDemoResetSchedule, DemoResetSchedule>();
        services.AddScoped<IDemoAccountGuard, DemoAccountGuard>();

        ArgumentNullException.ThrowIfNull(demo);

        if (!demo.Enabled)
        {
            services.AddScoped<IDemoDataManager, DisabledDemoDataManager>();
            return services;
        }

        // The demo's accounts are created through the local identity store. Where the sign-on
        // service owns people there is nothing to seed and nobody could sign in as the published
        // accounts, so say so at startup instead of publishing credentials that do not work.
        if (!ownsItsUsers)
        {
            throw new InvalidOperationException(
                $"{DemoOptions.SectionName}:{nameof(DemoOptions.Enabled)} requires Auth:Mode=local: "
                + "the published demo accounts are created in this product's own account store.");
        }

        services.AddSingleton<DemoCardState>();
        services.AddSingleton<DemoStoredCredentialState>();
        services.AddScoped<IDemoDataManager, DemoDataManager>();

        services.Replace(ServiceDescriptor.Scoped<ICardPaymentGateway, DemoCardPaymentGateway>());
        services.Replace(ServiceDescriptor.Scoped<IPaymentPluginResolver, DemoPaymentPluginResolver>());
        services.Replace(ServiceDescriptor.Scoped<IRegistrarProvider, DemoRegistrarProvider>());
        services.Replace(ServiceDescriptor.Scoped<IRegistrarProviderFactory, DemoRegistrarProviderFactory>());
        services.Replace(ServiceDescriptor.Scoped<IProvisioningProvider, DemoProvisioningProvider>());
        services.Replace(ServiceDescriptor.Scoped<IProvisioningProviderFactory, DemoProvisioningProviderFactory>());
        services.Replace(ServiceDescriptor.Scoped<IServerConnectionTester, DemoServerConnectionTester>());
        services.Replace(ServiceDescriptor.Scoped<IExchangeRateSource, DemoExchangeRateSource>());
        services.Replace(ServiceDescriptor.Scoped<IContactNotifier, DemoContactNotifier>());
        services.Replace(ServiceDescriptor.Scoped<IHostingProbe, DemoHostingProbe>());

        // The monitoring decorators read the stored results through the real services.
        services.AddScoped<SslMonitoringService>();
        services.AddScoped<DiskUsageService>();
        services.Replace(ServiceDescriptor.Scoped<ISslMonitoringService, DemoSslMonitoringService>());
        services.Replace(ServiceDescriptor.Scoped<IDiskUsageService, DemoDiskUsageService>());

        return services;
    }
}
