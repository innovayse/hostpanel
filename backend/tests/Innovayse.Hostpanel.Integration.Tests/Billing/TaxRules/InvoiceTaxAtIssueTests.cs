using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.InvoiceClientBillableItems;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.TaxRules;

/// <summary>
/// The tax a new invoice is raised with: the rate of the client's country is fixed on the invoice when it is
/// issued, a later change of the rule leaves it alone, and an exempt client is not taxed.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InvoiceTaxAtIssueTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin invoice is taxed at the client's country rate, and editing or deleting the rule afterwards does not change it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminInvoice_TakesTheCountryRate_AndKeepsItWhenTheRuleChanges()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var ruleId = await TaxRuleTestSeeds.CreateAsync(admin, "HU", null, 27m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "HU");

        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        (await admin.PutAsJsonAsync($"{TaxRuleTestSeeds.Route}/{ruleId}", TaxRuleTestSeeds.Body("HU", null, 5m))).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"{TaxRuleTestSeeds.Route}/{ruleId}")).EnsureSuccessStatusCode();

        using var invoice = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/{invoiceId}"));
        var amounts = invoice.RootElement.GetProperty("amounts");
        amounts.GetProperty("taxRate").GetDecimal().Should().Be(27m);
        amounts.GetProperty("tax").GetDecimal().Should().Be(27m);
        amounts.GetProperty("total").GetDecimal().Should().Be(127m);
    }

    /// <summary>An exempt client's invoice carries no tax although the country has a rule.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminInvoice_OfAnExemptClient_IsNotTaxed()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "RO", null, 19m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "RO", taxExempt: true);

        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        using var invoice = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/{invoiceId}"));
        invoice.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(100m);
    }

    /// <summary>The invoice an order raises is taxed at the client's country rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderInvoice_TakesTheCountryRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "BG", null, 20m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "BG");

        Result<int> result;
        using (var scope = factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            result = await bus.InvokeAsync<Result<int>>(
                new CreateOrderInvoiceCommand(clientId, "USD", [new OrderInvoiceItemDto("Starter Hosting", 50m, 2, null, 0m)]));
        }

        result.IsSuccess.Should().BeTrue();
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == result.Value);
        invoice.TaxRate.Should().Be(20m);
        invoice.Tax.Should().Be(20m);
        invoice.Total.Should().Be(120m);
    }

    /// <summary>The invoice raised for selected billable items is taxed at the client's country rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SelectedBillableItems_TakeTheCountryRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "LV", null, 21m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "LV");
        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "One-off charge",
            amount = 100m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.PostAsJsonAsync(
            $"/api/clients/{clientId}/billable-items/invoice-selected", new { billableItemIds = new[] { itemId } });

        response.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.ClientId == clientId);
        invoice.TaxRate.Should().Be(21m);
        invoice.Total.Should().Be(121m);
    }

    /// <summary>The invoice raised for a client's due billable items is taxed at the client's country rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DueBillableItems_TakeTheCountryRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "EE", null, 22m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "EE");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.BillableItems.Add(BillableItem.Create(
            clientId, "Consulting", 100m, "USD", BillableItemType.OneTime, recurringPeriod: null, nextDueDate: DateTimeOffset.UtcNow.AddDays(-1)));
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new InvoiceClientBillableItemsCommand(clientId, BillableItemType.OneTime), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.ClientId == clientId);
        invoice.TaxRate.Should().Be(22m);
        invoice.Total.Should().Be(122m);
    }

    /// <summary>A quote converted to an invoice is taxed at the client's country rate.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConvertedQuote_TakesTheCountryRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await TaxRuleTestSeeds.CreateAsync(admin, "HR", null, 25m);
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, "HR");
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 40m);

        var converted = await admin.PostAsync($"/api/billing/quotes/{quoteId}/convert-to-invoice", null);

        converted.EnsureSuccessStatusCode();
        var invoiceId = await converted.Content.ReadFromJsonAsync<int>();
        using var invoice = JsonDocument.Parse(await admin.GetStringAsync($"/api/billing/{invoiceId}"));
        invoice.RootElement.GetProperty("amounts").GetProperty("total").GetDecimal().Should().Be(50m);
    }
}
