namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Moq;
using Xunit;

/// <summary>
/// The checkout offers only what the admin has switched on and the deployment can take.
/// </summary>
/// <remarks>
/// Every case here was a live defect: bank transfer was listed with no gate at all, and
/// Stripe was gated on the secret key alone, so an "Inactive" card on the admin integrations
/// page still showed up at checkout.
/// </remarks>
public sealed class ListAvailablePaymentMethodsHandlerTests
{
    private readonly Mock<IPluginRegistry> plugins = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<ISettingRepository> settings = new();
    private readonly Mock<IStripeService> stripe = new();

    /// <summary>No plugins loaded unless a test says otherwise (Moq's default for the collection is null).</summary>
    public ListAvailablePaymentMethodsHandlerTests() =>
        plugins.Setup(p => p.GetLoadedManifests()).Returns([]);

    private ListAvailablePaymentMethodsHandler Handler() =>
        new(plugins.Object, resolver.Object, settings.Object, stripe.Object);

    /// <summary>Stores the admin's on/off switch for one integration.</summary>
    private void SwitchIntegration(string slug, bool enabled) =>
        settings.Setup(s => s.FindByKeyAsync(IntegrationSettingKeys.EnabledKey(slug), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Setting.Create(IntegrationSettingKeys.EnabledKey(slug), enabled ? "true" : "false", null));

    private static PluginManifest PaymentManifest(string id, string name) => new()
    {
        Id = id, Name = name, Version = "1.0.0", Author = "test", Description = "test",
        Type = PluginType.Payment, Category = "Payment Gateways", EntryPoint = "X", SdkVersion = "1.0",
    };

    private async Task<IReadOnlyList<string>> ListedModulesAsync()
    {
        var methods = await Handler().HandleAsync(new ListAvailablePaymentMethodsQuery(), CancellationToken.None);
        return methods.Select(m => m.Module).ToList();
    }

    /// <summary>The bug from the screenshot: only one gateway active, yet bank transfer offered.</summary>
    [Fact]
    public async Task BankTransferIsNotOfferedWhenTheAdminHasItOff()
    {
        SwitchIntegration(BuiltInPaymentModules.BankTransferIntegrationSlug, enabled: false);

        Assert.DoesNotContain(BuiltInPaymentModules.BankTransfer, await ListedModulesAsync());
    }

    /// <summary>A switch that was never saved reads as off, the way the admin list reads it.</summary>
    [Fact]
    public async Task BankTransferIsNotOfferedWhenTheSwitchWasNeverSaved()
    {
        Assert.DoesNotContain(BuiltInPaymentModules.BankTransfer, await ListedModulesAsync());
    }

    [Fact]
    public async Task BankTransferIsOfferedWhenTheAdminHasItOn()
    {
        SwitchIntegration(BuiltInPaymentModules.BankTransferIntegrationSlug, enabled: true);

        Assert.Contains(BuiltInPaymentModules.BankTransfer, await ListedModulesAsync());
    }

    /// <summary>A secret key alone is not consent: the admin's switch still decides.</summary>
    [Fact]
    public async Task StripeIsNotOfferedWhenConfiguredButSwitchedOff()
    {
        stripe.SetupGet(s => s.IsConfigured).Returns(true);
        SwitchIntegration(BuiltInPaymentModules.StripeIntegrationSlug, enabled: false);

        Assert.DoesNotContain(BuiltInPaymentModules.Stripe, await ListedModulesAsync());
    }

    /// <summary>The switch alone is not enough either: without a key the first call would fail.</summary>
    [Fact]
    public async Task StripeIsNotOfferedWhenSwitchedOnButNotConfigured()
    {
        stripe.SetupGet(s => s.IsConfigured).Returns(false);
        SwitchIntegration(BuiltInPaymentModules.StripeIntegrationSlug, enabled: true);

        Assert.DoesNotContain(BuiltInPaymentModules.Stripe, await ListedModulesAsync());
    }

    [Fact]
    public async Task StripeIsOfferedWhenConfiguredAndSwitchedOn()
    {
        stripe.SetupGet(s => s.IsConfigured).Returns(true);
        SwitchIntegration(BuiltInPaymentModules.StripeIntegrationSlug, enabled: true);

        Assert.Contains(BuiltInPaymentModules.Stripe, await ListedModulesAsync());
    }

    /// <summary>A plugin the resolver refuses is not listed; one it resolves is, under its manifest name.</summary>
    [Fact]
    public async Task PluginsFollowTheResolver()
    {
        plugins.Setup(p => p.GetLoadedManifests())
            .Returns([PaymentManifest("innovayse-inecobank", "Inecobank"), PaymentManifest("other-bank", "Other")]);
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IPaymentPlugin>());
        resolver.Setup(r => r.ResolveAsync("other-bank", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        var methods = await Handler().HandleAsync(new ListAvailablePaymentMethodsQuery(), CancellationToken.None);

        var only = Assert.Single(methods);
        Assert.Equal("innovayse-inecobank", only.Module);
        Assert.Equal("Bank Card (Inecobank)", only.DisplayName);
    }
}
