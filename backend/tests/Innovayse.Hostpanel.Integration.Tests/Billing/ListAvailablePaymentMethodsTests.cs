using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Constants;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The checkout method list (<c>GET /api/payment-methods</c>) against Postgres: the checkout
/// offers only what the admin has switched on and the deployment can take.
/// </summary>
/// <remarks>
/// Bank transfer's on/off switch is exercised through the real admin
/// integrations endpoint (our own code); the card gateway's <c>IsConfigured</c> and the loaded plugin set are
/// faked, since neither a live card payment gateway secret nor an on-disk plugin belongs in this suite. The
/// cases pin three gates: bank transfer is listed only when its switch is on, the card payment gateway needs
/// both its secret key and its switch, and a hosted gateway that charges in AMD is not offered to a client
/// billed in USD, who would otherwise be refused at Place Order with a currency-mismatch error.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListAvailablePaymentMethodsTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose card payment gateway configuration and loaded plugins come from the given fakes.</summary>
    /// <param name="cardGateway">The fake card payment gateway service the host should report through.</param>
    /// <param name="plugins">The fake plugin registry the host should report through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway cardGateway, FakePluginRegistry plugins) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ICardPaymentGateway>(cardGateway);
                services.AddSingleton<IPluginRegistry>(plugins);
            }));

    /// <summary>Switches an admin integration on or off through the real admin endpoint.</summary>
    /// <param name="host">The host to switch it on.</param>
    /// <param name="slug">The integration slug.</param>
    /// <param name="enabled">Whether it should end up enabled.</param>
    private async Task SwitchIntegrationAsync(WebApplicationFactory<Program> host, string slug, bool enabled)
    {
        var admin = host.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var response = await admin.PutAsJsonAsync(
            $"/api/admin/integrations/{slug}", new { isEnabled = enabled, config = new Dictionary<string, string>() });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Deletes every stored setting of an integration, so "never saved" is true however many other
    /// tests in the shared database have already switched it on or off.
    /// </summary>
    /// <param name="slug">The integration slug.</param>
    private async Task ForgetIntegrationSettingsAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var prefix = IntegrationSettingKeys.Prefix(slug);
        await context.Settings.Where(x => x.Key.StartsWith(prefix)).ExecuteDeleteAsync();
    }

    /// <summary>Builds a payment-plugin manifest for a test plugin.</summary>
    /// <param name="id">The module id.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest PaymentManifest(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Version = "1.0.0",
        Author = "test",
        Description = "test",
        Type = PluginType.Payment,
        Category = "Payment Gateways",
        EntryPoint = "X",
        SdkVersion = "1.0",
    };

    /// <summary>Lists the module ids available for <paramref name="currencyCode"/>, or the caller's own currency.</summary>
    /// <param name="client">The client to list with.</param>
    /// <param name="currencyCode">The currency to list for, or <see langword="null"/> for the caller's own.</param>
    /// <returns>The module ids offered.</returns>
    private static async Task<List<string>> ListedModulesAsync(HttpClient client, string? currencyCode = null)
    {
        var url = currencyCode is null ? "/api/payment-methods" : $"/api/payment-methods?currencyCode={currencyCode}";
        var methods = await client.GetFromJsonAsync<List<AvailablePaymentMethodDto>>(url);
        return methods!.Select(m => m.Module).ToList();
    }

    /// <summary>Bank transfer is not offered while the admin has it switched off.</summary>
    [Fact]
    public async Task BankTransferIsNotOfferedWhenTheAdminHasItOff()
    {
        var host = BuildHost(new FakeCardPaymentGateway(), new FakePluginRegistry());
        await SwitchIntegrationAsync(host, BuiltInPaymentModules.BankTransferIntegrationSlug, enabled: false);

        (await ListedModulesAsync(host.CreateClient())).Should().NotContain(BuiltInPaymentModules.BankTransfer);
    }

    /// <summary>A switch that was never saved reads as off, the way the admin list reads it.</summary>
    [Fact]
    public async Task BankTransferIsNotOfferedWhenTheSwitchWasNeverSaved()
    {
        var host = BuildHost(new FakeCardPaymentGateway(), new FakePluginRegistry());
        await ForgetIntegrationSettingsAsync(BuiltInPaymentModules.BankTransferIntegrationSlug);

        (await ListedModulesAsync(host.CreateClient())).Should().NotContain(BuiltInPaymentModules.BankTransfer);
    }

    /// <summary>Switching bank transfer on offers it.</summary>
    [Fact]
    public async Task BankTransferIsOfferedWhenTheAdminHasItOn()
    {
        var host = BuildHost(new FakeCardPaymentGateway(), new FakePluginRegistry());
        await SwitchIntegrationAsync(host, BuiltInPaymentModules.BankTransferIntegrationSlug, enabled: true);

        (await ListedModulesAsync(host.CreateClient())).Should().Contain(BuiltInPaymentModules.BankTransfer);
    }

    /// <summary>A secret key alone is not consent: the admin's switch still decides.</summary>
    [Fact]
    public async Task CardGatewayIsNotOfferedWhenConfiguredButSwitchedOff()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = true }, new FakePluginRegistry());
        await SwitchIntegrationAsync(host, BuiltInPaymentModules.StripeIntegrationSlug, enabled: false);

        (await ListedModulesAsync(host.CreateClient())).Should().NotContain(BuiltInPaymentModules.Stripe);
    }

    /// <summary>The switch alone is not enough either: without a key the first call would fail.</summary>
    [Fact]
    public async Task CardGatewayIsNotOfferedWhenSwitchedOnButNotConfigured()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = false }, new FakePluginRegistry());
        await SwitchIntegrationAsync(host, BuiltInPaymentModules.StripeIntegrationSlug, enabled: true);

        (await ListedModulesAsync(host.CreateClient())).Should().NotContain(BuiltInPaymentModules.Stripe);
    }

    /// <summary>Configured and switched on together, the card payment gateway is offered.</summary>
    [Fact]
    public async Task CardGatewayIsOfferedWhenConfiguredAndSwitchedOn()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = true }, new FakePluginRegistry());
        await SwitchIntegrationAsync(host, BuiltInPaymentModules.StripeIntegrationSlug, enabled: true);

        (await ListedModulesAsync(host.CreateClient())).Should().Contain(BuiltInPaymentModules.Stripe);
    }

    /// <summary>A plugin the resolver refuses is not listed; one it resolves is, under its manifest name.</summary>
    [Fact]
    public async Task PluginsFollowTheResolver()
    {
        var registry = new FakePluginRegistry();
        registry.Add(PaymentManifest("bank-gateway", "Bank Gateway"));
        registry.Add(PaymentManifest("other-bank", "Other"));
        var accepted = new FakePaymentPlugin { CurrencyCode = "840" }; // caller (anonymous) bills in the base, USD
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", accepted);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<ICardPaymentGateway, FakeCardPaymentGateway>();
                services.AddSingleton<IPluginRegistry>(registry);
                services.AddSingleton<IPaymentPluginResolver>(resolver);
            }));

        var methods = await host.CreateClient().GetFromJsonAsync<List<AvailablePaymentMethodDto>>("/api/payment-methods");

        // Only the plugin rows are this test's business. The built-in methods are switched on and
        // off by other tests against the same shared host and database, so asserting on the whole
        // list would pass or fail depending on which of them ran first.
        var plugins = methods!
            .Where(m => m.Module != BuiltInPaymentModules.BankTransfer
                        && m.Module != BuiltInPaymentModules.Stripe)
            .ToList();

        var only = Assert.Single(plugins);
        only.Module.Should().Be("bank-gateway");
        only.DisplayName.Should().Be("Bank Card (Bank Gateway)");
    }

    /// <summary>
    /// A gateway in another currency is not offered: a client billed in USD must not be offered an
    /// AMD-only gateway that Place Order would then refuse.
    /// </summary>
    [Fact]
    public async Task AGatewayInAnotherCurrencyIsNotOffered()
    {
        var registry = new FakePluginRegistry();
        registry.Add(PaymentManifest("bank-gateway", "Bank Gateway"));
        var plugin = new FakePaymentPlugin { CurrencyCode = "051" }; // AMD
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<ICardPaymentGateway, FakeCardPaymentGateway>();
                services.AddSingleton<IPluginRegistry>(registry);
                services.AddSingleton<IPaymentPluginResolver>(resolver);
            }));

        // Anonymous caller bills in the base currency, USD; the gateway charges in AMD.
        (await ListedModulesAsync(host.CreateClient())).Should().NotContain("bank-gateway");
    }

    /// <summary>A named currency the panel does not offer lists nothing, not the base's methods.</summary>
    [Fact]
    public async Task ANamedCurrencyNotOnOfferListsNothing()
    {
        var host = BuildHost(new FakeCardPaymentGateway(), new FakePluginRegistry());
        await SwitchIntegrationAsync(host, BuiltInPaymentModules.BankTransferIntegrationSlug, enabled: true);

        (await ListedModulesAsync(host.CreateClient(), "XXX")).Should().BeEmpty();
    }

    /// <summary>A named currency the panel does offer replaces the caller's own, even for an anonymous caller.</summary>
    [Fact]
    public async Task ANamedCurrencyReplacesTheCallersOwn()
    {
        var registry = new FakePluginRegistry();
        registry.Add(PaymentManifest("bank-gateway", "Bank Gateway"));
        var plugin = new FakePaymentPlugin { CurrencyCode = "051" }; // AMD
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<ICardPaymentGateway, FakeCardPaymentGateway>();
                services.AddSingleton<IPluginRegistry>(registry);
                services.AddSingleton<IPaymentPluginResolver>(resolver);
            }));

        // Anonymous caller's own currency is the base, USD; naming "amd" (any case) instead
        // makes the AMD gateway show up.
        (await ListedModulesAsync(host.CreateClient(), "amd")).Should().Contain("bank-gateway");
    }
}
