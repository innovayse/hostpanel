using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// Which adapters the container hands out: the offline fakes when <c>Demo:Enabled</c> is true, the
/// real registrations untouched when it is not, mail left alone either way, and a startup refusal
/// for an enabled demo with a missing value or without local accounts. Built on a bare service
/// collection, so no database is involved.
/// </summary>
public sealed class DemoServiceRegistrationTests
{
    /// <summary>A complete, valid set of demo settings.</summary>
    /// <returns>The settings as a mutable dictionary.</returns>
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Demo:Enabled"] = "true",
        ["Demo:ResetIntervalHours"] = "24",
        ["Demo:AdminEmail"] = DemoTestHost.AdminEmail,
        ["Demo:AdminPassword"] = DemoTestHost.AdminPassword,
        ["Demo:ClientEmail"] = DemoTestHost.ClientEmail,
        ["Demo:ClientPassword"] = DemoTestHost.ClientPassword,
    };

    /// <summary>Registers the demo on a bare collection.</summary>
    /// <param name="settings">The configuration values.</param>
    /// <param name="ownsItsUsers">Whether accounts live in the local store.</param>
    /// <returns>The collection and the provider built from it.</returns>
    private static BuiltServices Build(
        Dictionary<string, string?> settings, bool ownsItsUsers = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // The host registers its configuration as a service; the demo options bind from it.
        services.AddSingleton<IConfiguration>(configuration);
        var demo = configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();
        services.AddDemoMode(demo, ownsItsUsers);
        return new(services, services.BuildServiceProvider());
    }

    /// <summary>The implementation type registered for a service, or null when it is not registered.</summary>
    /// <typeparam name="TService">The service type to look up.</typeparam>
    /// <param name="services">The collection to inspect.</param>
    /// <returns>The implementation type of the last registration.</returns>
    private static Type? ImplementationOf<TService>(IServiceCollection services) =>
        services.LastOrDefault(d => d.ServiceType == typeof(TService))?.ImplementationType;

    /// <summary>Enabled: every outward port is registered to its demo fake (the descriptors are inspected, nothing is resolved).</summary>
    [Fact]
    public void WhenEnabled_EveryOutwardPortIsADemoFake()
    {
        var (services, _) = Build(ValidSettings());

        ImplementationOf<ICardPaymentGateway>(services).Should().Be<DemoCardPaymentGateway>();
        ImplementationOf<IPaymentPluginResolver>(services).Should().Be<DemoPaymentPluginResolver>();
        ImplementationOf<IRegistrarProvider>(services).Should().Be<DemoRegistrarProvider>();
        ImplementationOf<IRegistrarProviderFactory>(services).Should().Be<DemoRegistrarProviderFactory>();
        ImplementationOf<IProvisioningProvider>(services).Should().Be<DemoProvisioningProvider>();
        ImplementationOf<IProvisioningProviderFactory>(services).Should().Be<DemoProvisioningProviderFactory>();
        ImplementationOf<IServerConnectionTester>(services).Should().Be<DemoServerConnectionTester>();
        ImplementationOf<IExchangeRateSource>(services).Should().Be<DemoExchangeRateSource>();
        ImplementationOf<IContactNotifier>(services).Should().Be<DemoContactNotifier>();
        ImplementationOf<IHostingProbe>(services).Should().Be<DemoHostingProbe>();
        ImplementationOf<IDemoDataManager>(services).Should().Be<DemoDataManager>();
    }

    /// <summary>Enabled: the stored credential book is registered once, as a singleton.</summary>
    [Fact]
    public void WhenEnabled_TheStoredCredentialStateIsASingleton()
    {
        var (services, _) = Build(ValidSettings());

        services.Should().ContainSingle(d => d.ServiceType == typeof(DemoStoredCredentialState))
            .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    /// <summary>Disabled: the stored credential book is not registered.</summary>
    [Fact]
    public void WhenDisabled_TheStoredCredentialStateIsNotRegistered()
    {
        var (services, _) = Build(new Dictionary<string, string?>());

        services.Should().NotContain(d => d.ServiceType == typeof(DemoStoredCredentialState));
    }

    /// <summary>Enabled: mail is not faked, so it keeps going through the configured relay.</summary>
    [Fact]
    public void WhenEnabled_MailIsNotReplaced()
    {
        var (services, _) = Build(ValidSettings());

        services.Should().NotContain(d => d.ServiceType == typeof(IEmailSender));
    }

    /// <summary>Disabled: nothing is swapped and the data manager is the inert one.</summary>
    [Fact]
    public void WhenDisabled_NothingIsSwapped()
    {
        var (services, provider) = Build(new Dictionary<string, string?>());

        services.Should().NotContain(d => d.ServiceType == typeof(ICardPaymentGateway));
        services.Should().NotContain(d => d.ServiceType == typeof(IRegistrarProvider));
        ImplementationOf<IDemoDataManager>(services).Should().Be<DisabledDemoDataManager>();
        provider.GetRequiredService<IOptions<DemoOptions>>().Value.Enabled.Should().BeFalse();
    }

    /// <summary>Enabled without local accounts is refused at registration, before any credential is published.</summary>
    [Fact]
    public void WhenEnabledWithoutLocalAccounts_StartupIsRefused()
    {
        var act = () => Build(ValidSettings(), ownsItsUsers: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Auth:Mode=local*");
    }

    /// <summary>Enabled with a value missing: reading the options refuses, naming the key.</summary>
    [Theory]
    [InlineData("Demo:AdminEmail")]
    [InlineData("Demo:AdminPassword")]
    [InlineData("Demo:ClientEmail")]
    [InlineData("Demo:ClientPassword")]
    [InlineData("Demo:ResetIntervalHours")]
    public void WhenEnabledAndAValueIsMissing_TheOptionsAreRefused(string missingKey)
    {
        var settings = ValidSettings();
        settings.Remove(missingKey);
        var (_, provider) = Build(settings);

        var act = () => provider.GetRequiredService<IOptions<DemoOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage($"*{missingKey.Split(':')[1]}*");
    }

    /// <summary>The optional contact pair binds from the <c>Demo</c> section (<c>Demo__ContactEmail</c> / <c>Demo__ContactPhone</c> in env).</summary>
    [Fact]
    public void WhenEnabled_TheContactPairBinds()
    {
        var settings = ValidSettings();
        settings["Demo:ContactEmail"] = "contact@demo.example";
        settings["Demo:ContactPhone"] = "+1 555 0199";
        var (_, provider) = Build(settings);

        var options = provider.GetRequiredService<IOptions<DemoOptions>>().Value;

        options.ContactEmail.Should().Be("contact@demo.example");
        options.ContactPhone.Should().Be("+1 555 0199");
    }

    /// <summary>Unset, the contact pair is empty and startup is not refused: the seed keeps its fictional contact.</summary>
    [Fact]
    public void WhenEnabled_WithoutTheContactPair_TheOptionsAreValid()
    {
        var (_, provider) = Build(ValidSettings());

        var options = provider.GetRequiredService<IOptions<DemoOptions>>().Value;

        options.ContactEmail.Should().BeNull();
        options.ContactPhone.Should().BeNull();
    }

    /// <summary>A contact email that is set but is not an address refuses startup by name.</summary>
    [Fact]
    public void WhenEnabled_WithAMalformedContactEmail_TheOptionsAreRefused()
    {
        var settings = ValidSettings();
        settings["Demo:ContactEmail"] = "not-an-address";
        var (_, provider) = Build(settings);

        var act = () => provider.GetRequiredService<IOptions<DemoOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*ContactEmail*");
    }
}
