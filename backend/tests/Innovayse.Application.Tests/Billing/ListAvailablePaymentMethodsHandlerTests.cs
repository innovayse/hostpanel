namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Options;
using Innovayse.Application.Billing.Queries.ListAvailablePaymentMethods;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>
/// The checkout offers only what the admin has switched on and the deployment can take.
/// </summary>
/// <remarks>
/// Every case here was a live defect: bank transfer was listed with no gate at all, Stripe was
/// gated on the secret key alone, so an "Inactive" card on the admin integrations page still
/// showed up at checkout -- and a hosted gateway that charges in AMD was offered to a client
/// billed in USD, who then pressed Place Order and got a currency-mismatch error.
/// </remarks>
public sealed class ListAvailablePaymentMethodsHandlerTests
{
    private readonly Mock<IPluginRegistry> plugins = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<ISettingRepository> settings = new();
    private readonly Mock<IStripeService> stripe = new();
    private readonly Mock<ICurrentRequestContext> caller = new();
    private readonly Mock<IClientRepository> clientRepo = new();

    /// <summary>Panel default currency; individual tests override it through <see cref="BilledIn"/>.</summary>
    private string defaultCurrency = "AMD";

    /// <summary>No plugins loaded unless a test says otherwise (Moq's default for the collection is null).</summary>
    public ListAvailablePaymentMethodsHandlerTests() =>
        plugins.Setup(p => p.GetLoadedManifests()).Returns([]);

    private ListAvailablePaymentMethodsHandler Handler() =>
        new(plugins.Object, resolver.Object, settings.Object, stripe.Object,
            caller.Object, clientRepo.Object, Options.Create(new BillingOptions { DefaultCurrency = defaultCurrency }));

    /// <summary>Makes the caller a signed-in client billed in <paramref name="currency"/> (null = no currency recorded).</summary>
    private void SignedInClientBilledIn(string? currency)
    {
        var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com");
        client.UpdatePreferences(currency, null, null, null);
        caller.SetupGet(c => c.UserId).Returns("user-1");
        clientRepo.Setup(r => r.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(client);
    }

    /// <summary>Makes the caller anonymous, as a guest at checkout is.</summary>
    private void Guest() => caller.SetupGet(c => c.UserId).Returns((string?)null);

    /// <summary>Sets the panel default currency.</summary>
    private void BilledIn(string currency) => defaultCurrency = currency;

    /// <summary>Loads one payment plugin that the resolver accepts and that charges in <paramref name="numericCurrency"/>.</summary>
    private void LoadedGateway(string id, string name, string numericCurrency)
    {
        plugins.Setup(p => p.GetLoadedManifests()).Returns([PaymentManifest(id, name)]);
        var plugin = new Mock<IPaymentPlugin>();
        plugin.SetupGet(p => p.CurrencyCode).Returns(numericCurrency);
        resolver.Setup(r => r.ResolveAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(plugin.Object);
    }

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
        Guest();
        plugins.Setup(p => p.GetLoadedManifests())
            .Returns([PaymentManifest("inecobank", "Inecobank"), PaymentManifest("other-bank", "Other")]);
        var accepted = new Mock<IPaymentPlugin>();
        accepted.SetupGet(p => p.CurrencyCode).Returns("051");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(accepted.Object);
        resolver.Setup(r => r.ResolveAsync("other-bank", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        var methods = await Handler().HandleAsync(new ListAvailablePaymentMethodsQuery(), CancellationToken.None);

        var only = Assert.Single(methods);
        Assert.Equal("inecobank", only.Module);
        Assert.Equal("Bank Card (Inecobank)", only.DisplayName);
    }

    /// <summary>
    /// The screenshot: a client billed in USD was offered an AMD-only gateway, picked it, and
    /// was refused at Place Order. The refusal is right; the offer was the defect.
    /// </summary>
    [Fact]
    public async Task AGatewayInAnotherCurrencyIsNotOfferedToASignedInClient()
    {
        SignedInClientBilledIn("USD");
        LoadedGateway("inecobank", "Inecobank", "051");

        Assert.DoesNotContain("inecobank", await ListedModulesAsync());
    }

    /// <summary>A client billed in the gateway's currency sees it.</summary>
    [Fact]
    public async Task AGatewayInTheClientsCurrencyIsOffered()
    {
        SignedInClientBilledIn("AMD");
        LoadedGateway("inecobank", "Inecobank", "051");

        Assert.Contains("inecobank", await ListedModulesAsync());
    }

    /// <summary>
    /// A client with no currency recorded is billed in the panel default -- the same rule the
    /// start uses -- so the default decides what they see.
    /// </summary>
    [Fact]
    public async Task AClientWithNoCurrencyFallsBackToThePanelDefault()
    {
        BilledIn("USD");
        SignedInClientBilledIn(null);
        LoadedGateway("inecobank", "Inecobank", "051");

        Assert.DoesNotContain("inecobank", await ListedModulesAsync());
    }

    /// <summary>A guest has no client record; the panel default is the currency their order will carry.</summary>
    [Fact]
    public async Task AGuestIsBilledInThePanelDefault()
    {
        BilledIn("AMD");
        Guest();
        LoadedGateway("inecobank", "Inecobank", "051");

        Assert.Contains("inecobank", await ListedModulesAsync());
    }

    /// <summary>
    /// A currency with no ISO numeric mapping cannot go through any gateway -- the start would
    /// refuse it -- so no gateway is offered rather than one that will fail.
    /// </summary>
    [Fact]
    public async Task AnUnmappedCurrencyGetsNoGateway()
    {
        SignedInClientBilledIn("XYZ");
        LoadedGateway("inecobank", "Inecobank", "051");

        Assert.DoesNotContain("inecobank", await ListedModulesAsync());
    }
}
