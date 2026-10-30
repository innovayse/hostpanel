using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.SDK.Plugins.Fulfilment;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/refund</c> with a fulfilment plugin enabled: the plugin is asked before any
/// money moves, the refund is capped at its answer with its reason in the notes, a plugin that cannot
/// answer refuses the refund, and without a plugin the refund is untouched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RefundInvoicePaymentPluginAdviceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The price of the invoices in these tests.</summary>
    private const decimal Price = 10m;

    /// <summary>Creates an invoice of <see cref="Price"/> on the host and pays it in full manually.</summary>
    /// <param name="host">The host to run against.</param>
    /// <param name="admin">An admin HTTP client of that host.</param>
    /// <returns>The paid invoice id.</returns>
    private async Task<int> PaidInvoiceAsync(WebApplicationFactory<Program> host, HttpClient admin)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, Price);
        var paid = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = Price,
            fees = 0m,
        });
        paid.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return invoiceId;
    }

    /// <summary>Asks for a manual refund of <paramref name="amount"/> with the operator's note.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The amount asked for.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> RefundAsync(HttpClient admin, int invoiceId, decimal amount) =>
        admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.BankTransfer,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
            notes = "Customer asked",
        });

    /// <summary>Reads the refund transactions recorded on an invoice, oldest first.</summary>
    /// <param name="host">The host whose database to read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The refund transactions.</returns>
    private static async Task<List<InvoiceTransaction>> RefundRowsAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoiceTransactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.Type == InvoiceTransactionType.Refund)
            .OrderBy(t => t.Id)
            .ToListAsync();
    }

    /// <summary>A plugin that caps the refund lowers the amount and its reason is written to the notes.</summary>
    [Fact]
    public async Task PluginCapReducesTheRefundAndWritesItsReasonToTheNotes()
    {
        using var scenario = new FulfilmentScenario(factory);
        scenario.Plugin.Advice = (_, _) => Task.FromResult(new RefundAdvice(4m, "Licences already issued."));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var invoiceId = await PaidInvoiceAsync(scenario.Host, admin);

        var response = await RefundAsync(admin, invoiceId, 8m);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        scenario.Plugin.LastAdvised.Should().NotBeNull();
        scenario.Plugin.LastAdvised!.RequestedAmount.Should().Be(8m);
        var row = (await RefundRowsAsync(scenario.Host, invoiceId)).Should().ContainSingle().Subject;
        row.Amount.Should().Be(4m);
        row.Notes.Should().Contain("Customer asked").And.Contain(scenario.PluginId).And.Contain("Licences already issued.");
    }

    /// <summary>A plugin that allows the whole amount changes neither the refund nor its notes.</summary>
    [Fact]
    public async Task PluginAllowingTheAmountLeavesTheRefundUnchanged()
    {
        using var scenario = new FulfilmentScenario(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var invoiceId = await PaidInvoiceAsync(scenario.Host, admin);

        var response = await RefundAsync(admin, invoiceId, 8m);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        scenario.Plugin.LastAdvised.Should().NotBeNull();
        var row = (await RefundRowsAsync(scenario.Host, invoiceId)).Should().ContainSingle().Subject;
        row.Amount.Should().Be(8m);
        row.Notes.Should().Be("Customer asked");
    }

    /// <summary>A plugin that allows nothing refuses the refund and nothing is recorded.</summary>
    [Fact]
    public async Task PluginAllowingNothingRefusesTheRefund()
    {
        using var scenario = new FulfilmentScenario(factory);
        scenario.Plugin.Advice = (_, _) => Task.FromResult(new RefundAdvice(0m, "Subscription is active."));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var invoiceId = await PaidInvoiceAsync(scenario.Host, admin);

        var response = await RefundAsync(admin, invoiceId, 8m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REFUND_BLOCKED_BY_PLUGIN);
        (await RefundRowsAsync(scenario.Host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>A plugin that throws leaves the refund refused, fail closed, with nothing recorded.</summary>
    [Fact]
    public async Task PluginThatThrowsRefusesTheRefund()
    {
        using var scenario = new FulfilmentScenario(factory);
        scenario.Plugin.Advice = (_, _) => throw new InvalidOperationException("boom");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var invoiceId = await PaidInvoiceAsync(scenario.Host, admin);

        var response = await RefundAsync(admin, invoiceId, 8m);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REFUND_ADVICE_UNAVAILABLE);
        (await RefundRowsAsync(scenario.Host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>A plugin that does not answer within the call timeout refuses the refund, like one that throws.</summary>
    [Fact]
    public async Task PluginThatTimesOutRefusesTheRefund()
    {
        using var scenario = new FulfilmentScenario(factory, new Dictionary<string, string> { ["FulfilmentCallTimeoutSeconds"] = "1" });
        scenario.Plugin.Advice = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new RefundAdvice(Price, null);
        };
        var admin = await BillingTestSeeds.AdminClientAsync(factory, scenario.Host);
        var invoiceId = await PaidInvoiceAsync(scenario.Host, admin);

        var response = await RefundAsync(admin, invoiceId, 8m);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REFUND_ADVICE_UNAVAILABLE);
        (await RefundRowsAsync(scenario.Host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>With no fulfilment plugin enabled nobody is asked and the refund goes through as requested.</summary>
    [Fact]
    public async Task NoEnabledPluginLeavesTheRefundUnchanged()
    {
        var resolver = new FakeFulfilmentPluginResolver();
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFulfilmentPluginResolver>();
            services.AddSingleton<IFulfilmentPluginResolver>(resolver);
        }));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await PaidInvoiceAsync(host, admin);

        var response = await RefundAsync(admin, invoiceId, 8m);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var row = (await RefundRowsAsync(host, invoiceId)).Should().ContainSingle().Subject;
        row.Amount.Should().Be(8m);
        row.Notes.Should().Be("Customer asked");
    }
}
