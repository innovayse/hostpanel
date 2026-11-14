using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builders and readers for the invoice list tests: invoices seeded straight into the database at a chosen moment, with a
/// chosen payment method and status, and readers for the ids a page holds and for the fields a refusal names.
/// </summary>
public static class InvoiceListTestSeeds
{
    /// <summary>Seeds one invoice of one line for a client, created at the given moment.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="createdAt">The moment the invoice was created (UTC); now when not given.</param>
    /// <param name="paymentMethod">The payment method stored on the invoice, or null for none.</param>
    /// <param name="isDraft">Whether the invoice is a draft rather than unpaid.</param>
    /// <returns>The new invoice id.</returns>
    public static async Task<int> SeedAsync(
        WebApplicationFactory<Program> host,
        int clientId,
        DateTimeOffset? createdAt = null,
        string? paymentMethod = null,
        bool isDraft = false)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var created = createdAt ?? DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(clientId, created.AddDays(14), "USD", isDraft, created);
        invoice.AddItem("Hosting", 10m, 1, null);
        invoice.UpdateOptions(created, created.AddDays(14), paymentMethod, 0m);
        invoice.Backdate(created);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>
    /// Builds a text that is unique to one test and holds no digit, so a search for it can never match an invoice id and a
    /// search for an id can never match it.
    /// </summary>
    /// <returns>Thirty-two lower-case letters.</returns>
    public static string UniqueToken() => string.Concat(Guid.NewGuid().ToByteArray().SelectMany(b => new[] { (char)('a' + (b >> 4)), (char)('a' + (b & 15)) }));

    /// <summary>Reads a page of an invoice list that must have been answered with 200.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="url">The list address with its query string.</param>
    /// <returns>The page body.</returns>
    public static async Task<JsonElement> PageAsync(HttpClient admin, string url)
    {
        var response = await admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>Reads the ids of the rows of a page, in the order the page holds them.</summary>
    /// <param name="page">The page body.</param>
    /// <returns>The row ids.</returns>
    public static List<int> IdsOf(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();

    /// <summary>Reads the names of the fields a 400 refusal reports, in lower case.</summary>
    /// <param name="response">The refused response.</param>
    /// <returns>The field names the refusal carries.</returns>
    public static async Task<List<string>> RefusedFieldsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();
    }
}
