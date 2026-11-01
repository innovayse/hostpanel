using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo payment gateway accepts every currency the shop stores, so an AMD invoice starts a
/// payment as readily as a USD one; a deployment that is not a demo keeps its single-currency plugin.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoPaymentCurrencyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>ISO 4217 numeric code of AMD.</summary>
    private const string AmdNumeric = "051";

    /// <summary>Makes sure the AMD currency is stored, as the demo seed stores it.</summary>
    /// <param name="host">The host whose database to prepare.</param>
    private static async Task EnsureAmdAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Currencies.AnyAsync(c => c.Code == "AMD"))
        {
            db.Currencies.Add(Currency.Create("AMD", AmdNumeric, string.Empty, " AMD", 0, 0.0026m, false));
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Demo on: an AMD invoice starts a payment through the demo gateway.</summary>
    [Fact]
    public async Task WhenEnabled_AnAmdInvoiceStartsAPaymentAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await EnsureAmdAsync(host);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "demo-amd");
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Clients.FirstAsync(c => c.Id == clientId)).SetCurrency("AMD");
            await db.SaveChangesAsync();
        }

        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 5000m);

        var response = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = BillingTestSeeds.AllowedReturnUrl });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Demo on: the resolved gateway accepts every stored currency and nothing else.</summary>
    [Fact]
    public async Task WhenEnabled_TheGatewayAcceptsEveryStoredCurrencyAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        await EnsureAmdAsync(host);

        using var scope = host.Services.CreateScope();
        var plugin = await scope.ServiceProvider.GetRequiredService<IPaymentPluginResolver>()
            .ResolveAsync("any-module", CancellationToken.None);

        plugin!.AcceptsCurrency(AmdNumeric).Should().BeTrue();
        plugin.AcceptsCurrency("840").Should().BeTrue();
        plugin.AcceptsCurrency("978").Should().BeFalse();
    }

    /// <summary>Demo off: a plugin that does not override the check still accepts only its own currency.</summary>
    [Fact]
    public void WhenDisabled_APluginAcceptsOnlyItsOwnCurrency()
    {
        Innovayse.SDK.Plugins.Payment.IPaymentPlugin plugin = new FakePaymentPlugin { CurrencyCode = "840" };

        plugin.AcceptsCurrency("840").Should().BeTrue();
        plugin.AcceptsCurrency(AmdNumeric).Should().BeFalse();
    }
}
