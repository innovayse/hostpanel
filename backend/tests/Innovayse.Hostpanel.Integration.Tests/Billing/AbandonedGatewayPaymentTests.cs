using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// A hosted-gateway payment the host abandons is voided at the gateway when its plugin can cancel
/// (<c>IPaymentCancelPlugin</c>): the session of a cancelled invoice, and the session a new attempt replaces. A plugin that
/// cannot cancel, or that throws, changes nothing for the caller: the cancel and the new attempt still succeed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AbandonedGatewayPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The plugin id every test registers its fake under.</summary>
    private const string Module = "bank-gateway";

    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver)
    {
        return factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
    }

    /// <summary>Gives an unpaid invoice a hosted session, as an earlier attempt would have left it.</summary>
    /// <param name="host">The host whose database holds the invoice.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="orderId">The gateway order of the session.</param>
    /// <param name="startedAt">When the session was registered.</param>
    /// <returns>A task that completes when the session is saved.</returns>
    private static async Task SeedSessionAsync(WebApplicationFactory<Program> host, int invoiceId, string orderId, DateTimeOffset startedAt)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        invoice.SetGatewaySession(Module, orderId, startedAt);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Cancelling an invoice that holds a hosted session asks the plugin to cancel that order.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancellingAnInvoice_CancelsItsOpenSessionAtTheGateway()
    {
        var plugin = new FakeCancelPaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, await BillingTestSeeds.SeedClientAsync(host), 10m);
        await SeedSessionAsync(host, invoiceId, "gw-open-1", DateTimeOffset.UtcNow);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.CancelledOrders.Should().Equal("gw-open-1");
    }

    /// <summary>A plugin without the cancel capability leaves the cancel as it was: it succeeds and nothing is asked.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancellingAnInvoice_WithAPluginThatCannotCancel_JustSucceeds()
    {
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, await BillingTestSeeds.SeedClientAsync(host), 10m);
        await SeedSessionAsync(host, invoiceId, "gw-open-2", DateTimeOffset.UtcNow);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.GetStatusCallCount.Should().Be(0);
    }

    /// <summary>An invoice with no hosted session is cancelled without asking any plugin.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancellingAnInvoice_WithNoSession_AsksNoPlugin()
    {
        var plugin = new FakeCancelPaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, await BillingTestSeeds.SeedClientAsync(host), 10m);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.CancelledOrders.Should().BeEmpty();
    }

    /// <summary>A plugin that throws while cancelling does not undo or fail the invoice cancel.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancellingAnInvoice_WhenThePluginThrows_StillCancels()
    {
        var plugin = new FakeCancelPaymentPlugin { CancelThrows = new InvalidOperationException("gateway down") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, await BillingTestSeeds.SeedClientAsync(host), 10m);
        await SeedSessionAsync(host, invoiceId, "gw-open-3", DateTimeOffset.UtcNow);

        var response = await admin.PostAsync($"/api/billing/{invoiceId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.CancelledOrders.Should().Equal("gw-open-3");
    }

    /// <summary>A new attempt that replaces an older, expired session cancels the older order and keeps the new one.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StartingANewAttempt_CancelsTheSessionItReplaces()
    {
        var plugin = new FakeCancelPaymentPlugin { NextOrderId = "gw-new-1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-abandon-1");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await SeedSessionAsync(host, invoiceId, "gw-old-1", DateTimeOffset.UtcNow.AddHours(-2));

        var response = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start", new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        plugin.CancelledOrders.Should().Equal("gw-old-1");
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId)).GatewayOrderId.Should().Be("gw-new-1");
    }

    /// <summary>A first attempt, with no session to replace, cancels nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StartingTheFirstAttempt_CancelsNothing()
    {
        var plugin = new FakeCancelPaymentPlugin { NextOrderId = "gw-first" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-abandon-2");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start", new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        plugin.CancelledOrders.Should().BeEmpty();
    }

    /// <summary>A plugin that throws on the old order never costs the payer the new attempt.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StartingANewAttempt_WhenCancellingTheOldOneThrows_StillStarts()
    {
        var plugin = new FakeCancelPaymentPlugin { NextOrderId = "gw-new-3", CancelThrows = new InvalidOperationException("gateway down") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Module, plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-abandon-3");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await SeedSessionAsync(host, invoiceId, "gw-old-3", DateTimeOffset.UtcNow.AddHours(-2));

        var response = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start", new { module = Module, returnUrl = BillingTestSeeds.AllowedReturnUrl });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        plugin.CancelledOrders.Should().Equal("gw-old-3");
    }
}
