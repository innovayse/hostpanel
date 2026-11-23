using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The payment half of the host API a plugin is handed (<see cref="IPluginHostPaymentApi"/>), against Postgres with the payment
/// plugin replaced by <see cref="FakePaymentPluginResolver"/>: the built host API offers it, and a request to read an order
/// reaches the use case under the call's own plugin id, so a plugin can only ever ask about the orders it owns.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginHostPaymentApiTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The plugin id the seeded sessions belong to.</summary>
    private const string Gateway = "bank-gateway";

    /// <summary>The host API a call is handed can be asked to read an order: a type test on it succeeds, as the SDK documents.</summary>
    [Fact]
    public void BuiltHostApi_IsThePaymentApi()
    {
        using var scope = factory.Services.CreateScope();

        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>()
            .Create(PluginCallBinding.WithoutClient($"payment-api-{Guid.NewGuid():N}"));

        (services.Host is IPluginHostPaymentApi).Should().BeTrue();
    }

    /// <summary>A callback's request settles the order of the call's own plugin, through the status read.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RequestOrderRead_SettlesTheOrderOfTheCallsPlugin()
    {
        var plugin = PaidPlugin();
        var host = BuildHost(plugin, Gateway);
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        await RequestAsync(host, Gateway, orderId);

        plugin.GetStatusCallCount.Should().Be(1);
        (await StatusOfAsync(host, invoiceId)).Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>A plugin that names another plugin's order reads nothing: the id comes from the call's binding, never from the request.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RequestOrderRead_NeverReadsAnotherPluginsOrder()
    {
        var plugin = PaidPlugin();
        var host = BuildHost(plugin, Gateway, "other-gateway");
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        await RequestAsync(host, "other-gateway", orderId);

        plugin.GetStatusCallCount.Should().Be(0);
        (await StatusOfAsync(host, invoiceId)).Should().NotBe(InvoiceStatus.Paid);
    }

    /// <summary>A plugin that reports the full 10.00 of its order paid.</summary>
    /// <returns>The plugin.</returns>
    private static FakePaymentPlugin PaidPlugin() =>
        new() { StatusResult = new(GatewayPaymentState.Paid, $"ref-{Guid.NewGuid():N}", "orderStatus:2", AmountMinor: 1000) };

    /// <summary>Builds a host whose payment plugins, under every id given, are <paramref name="plugin"/>.</summary>
    /// <param name="plugin">The fake plugin.</param>
    /// <param name="ids">The plugin ids that resolve to it.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPlugin plugin, params string[] ids)
    {
        var resolver = new FakePaymentPluginResolver();
        foreach (var id in ids)
        {
            resolver.Register(id, plugin);
        }

        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
    }

    /// <summary>Asks to read an order through the host API of a call bound to <paramref name="pluginId"/>, as the plugin's callback does.</summary>
    /// <param name="host">The host whose services build the call's host API.</param>
    /// <param name="pluginId">The plugin the call is bound to.</param>
    /// <param name="gatewayOrderId">The order the plugin names.</param>
    /// <returns>A task that completes once the request has been taken.</returns>
    private static async Task RequestAsync(WebApplicationFactory<Program> host, string pluginId, string gatewayOrderId)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(PluginCallBinding.WithoutClient(pluginId));
        await ((IPluginHostPaymentApi)services.Host).RequestOrderReadAsync(gatewayOrderId, CancellationToken.None);
    }

    /// <summary>Reads an invoice's status as the database holds it.</summary>
    /// <param name="host">The host whose database holds the invoice.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status.</returns>
    private static async Task<InvoiceStatus> StatusOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Invoices
            .AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status;
    }
}
