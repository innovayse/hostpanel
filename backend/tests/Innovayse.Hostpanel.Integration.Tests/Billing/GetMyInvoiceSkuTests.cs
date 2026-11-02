using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/invoices/{id}</c> and <c>GET /api/me/invoices</c> against Postgres: each line carries the SKU
/// of its product snapshot, and <see langword="null"/> when the line has none.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMyInvoiceSkuTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A product line carries the SKU of its snapshot.</summary>
    [Fact]
    public async Task GetById_ProductLine_CarriesTheSnapshotSku()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "sku-product");
        var invoiceId = await SeedAsync(clientId, invoice =>
            invoice.AddItem("Licence", 10m, 1, null, new ProductSnapshot(7, BillingCycle.Annual, true, "licence-pro", null)));

        var items = await ItemsAsync(owner, invoiceId);

        items.Single().GetProperty("sku").GetString().Should().Be("licence-pro");
    }

    /// <summary>A service renewal line carries the SKU of the renewed product.</summary>
    [Fact]
    public async Task GetById_RenewalLine_CarriesTheSnapshotSku()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "sku-renewal");
        var now = DateTimeOffset.UtcNow;
        var invoiceId = await SeedAsync(clientId, invoice =>
            invoice.AddServiceRenewal(
                "Hosting renewal", 20m, 5, now, now.AddYears(1), new ProductSnapshot(8, BillingCycle.Annual, false, "hosting-basic", null)));

        var items = await ItemsAsync(owner, invoiceId);

        items.Single().GetProperty("sku").GetString().Should().Be("hosting-basic");
    }

    /// <summary>A manual line and a domain renewal have no snapshot, so their SKU is null; the list says the same.</summary>
    [Fact]
    public async Task GetById_LinesWithoutSnapshot_CarryNullSku()
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "sku-manual");
        var now = DateTimeOffset.UtcNow;
        var invoiceId = await SeedAsync(clientId, invoice =>
        {
            invoice.AddItem("Setup fee", 5m, 1);
            invoice.AddDomainRenewal("example.test", 12m, 3, now, now.AddYears(1));
        });

        var single = await ItemsAsync(owner, invoiceId);
        var list = await owner.GetFromJsonAsync<JsonElement>("/api/me/invoices");
        var listed = list.EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == invoiceId).GetProperty("items");

        single.Should().HaveCount(2).And.OnlyContain(i => i.GetProperty("sku").ValueKind == JsonValueKind.Null);
        listed.EnumerateArray().Should().OnlyContain(i => i.GetProperty("sku").ValueKind == JsonValueKind.Null);
    }

    /// <summary>Saves an unpaid invoice for the client, its lines added by <paramref name="fill"/>.</summary>
    /// <param name="clientId">The owner.</param>
    /// <param name="fill">Adds the lines.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedAsync(int clientId, Action<Invoice> fill)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        fill(invoice);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Reads the lines of one of the client's own invoices.</summary>
    /// <param name="client">The signed-in owner.</param>
    /// <param name="invoiceId">The invoice id.</param>
    /// <returns>The line elements.</returns>
    private static async Task<List<JsonElement>> ItemsAsync(HttpClient client, int invoiceId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/me/invoices/{invoiceId}");
        return body.GetProperty("items").EnumerateArray().ToList();
    }
}
