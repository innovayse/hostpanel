using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared helpers for the VAT rate tests: the admin route, a valid request body, creating rows and reading the list, and
/// seeding the clients and the invoices the destination rate is decided on.
/// </summary>
internal static class VatRateTestSeeds
{
    /// <summary>The collection route of the VAT rates admin API.</summary>
    public const string Route = "/api/admin/vat-rates";

    /// <summary>A valid request body for creating or updating a row.</summary>
    /// <param name="country">The country code to send.</param>
    /// <param name="kind">The kind of rate to send, as the wire spells it.</param>
    /// <param name="ratePercent">The rate to send.</param>
    /// <param name="validFrom">The first day to send.</param>
    /// <returns>The request body.</returns>
    public static object Body(string country, string kind, decimal ratePercent, DateOnly validFrom) => new
    {
        country,
        kind,
        ratePercent,
        validFrom = validFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    /// <summary>Creates a row through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="country">The country code.</param>
    /// <param name="kind">The kind of rate, as the wire spells it.</param>
    /// <param name="ratePercent">The rate in percent.</param>
    /// <param name="validFrom">The first day the rate applies.</param>
    /// <returns>The new row's id.</returns>
    public static Task<int> CreateAsync(HttpClient admin, string country, string kind, decimal ratePercent, DateOnly validFrom) =>
        StorefrontContentTestSeeds.CreateAsync(admin, Route, Body(country, kind, ratePercent, validFrom));

    /// <summary>Reads one page of the list through the admin API.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="query">The query string, without the question mark.</param>
    /// <returns>The page as the API answers it.</returns>
    public static async Task<JsonElement> ListAsync(HttpClient admin, string query) =>
        await admin.GetFromJsonAsync<JsonElement>($"{Route}?{query}");

    /// <summary>Stores the seller's country setting.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="country">The two-letter code to store, or empty to switch the destination rates and the reverse charge off.</param>
    /// <returns>The value the setting held before, so a test can put it back.</returns>
    public static async Task<string> SetSellerCountryAsync(IntegrationTestFactory factory, string country)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.SellerCountry);
        var previous = row.Value;
        row.UpdateValue(country);
        await context.SaveChangesAsync(CancellationToken.None);
        return previous;
    }

    /// <summary>Seeds a client with a country and, optionally, a tax identification number.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="country">The client's country code.</param>
    /// <param name="taxId">The client's tax identification number, or null for a consumer.</param>
    /// <returns>The new client's id.</returns>
    public static async Task<int> SeedClientAsync(IntegrationTestFactory factory, string country, string? taxId = null)
    {
        var clientId = await TaxRuleTestSeeds.SeedClientAsync(factory, country);
        if (taxId is null)
        {
            return clientId;
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var client = await context.Clients.FirstAsync(c => c.Id == clientId);
        client.UpdateSettings(true, false, taxId, null, false, false, false, true, true);
        await context.SaveChangesAsync(CancellationToken.None);
        return clientId;
    }

    /// <summary>Builds an invoice for a client with one line per product snapshot, each flagged as a digital service or not.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="issuedAt">The invoice date.</param>
    /// <param name="digitalServiceLines">One entry per line: whether the line charges for a digital service.</param>
    /// <returns>The new, unsaved invoice.</returns>
    public static Invoice InvoiceOf(int clientId, DateTimeOffset issuedAt, params bool[] digitalServiceLines)
    {
        var invoice = Invoice.Create(clientId, issuedAt.AddDays(14), "EUR", issuedAt);
        foreach (var isDigitalService in digitalServiceLines)
        {
            invoice.AddItem(
                "Hosting",
                10m,
                1,
                null,
                new ProductSnapshot(1, BillingCycle.Monthly, false, null, null, null, isDigitalService));
        }

        return invoice;
    }
}
