using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Seeding and authentication helpers shared by every Reports use-case test file, so the seeding
/// logic exists exactly once rather than once per use case.
/// </summary>
public static class ReportsTestSeeds
{
    /// <summary>Shared test password for every registered client user.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>Builds a client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The factory whose admin credentials to mint a token from.</param>
    /// <returns>An authenticated admin <see cref="HttpClient"/>.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Registers a new client user over HTTP and logs them in: the "wrong role" caller every
    /// Reports use case refuses with 403, since the module's routes are admin only.
    /// </summary>
    /// <param name="factory">The factory to register and mint a token from.</param>
    /// <param name="label">Distinguishes the seeded email between callers in the same test.</param>
    /// <returns>An authenticated client carrying the Client role's bearer token.</returns>
    public static async Task<HttpClient> ClientRoleClientAsync(IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"reports-{label}-{Guid.NewGuid():N}@example.test";
        var registered = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Reports",
            lastName = label,
        });
        registered.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>Creates and persists a client with a unique, marked name.</summary>
    /// <param name="factory">The host to seed against.</param>
    /// <param name="marker">A unique token embedded in the first name so a test can find its own row.</param>
    /// <param name="city">City to record, or null.</param>
    /// <param name="country">Country to record, or null.</param>
    /// <returns>The persisted client's id.</returns>
    public static async Task<int> CreateClientAsync(
        IntegrationTestFactory factory, string marker, string? city = null, string? country = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create(Guid.NewGuid().ToString(), marker, "Reporting", $"{marker}@reports.test", TestMoments.Seeded);
        if (city is not null || country is not null)
        {
            client.UpdateAddress(client.Street, client.Address2, city ?? client.City, client.State, client.PostCode, country ?? client.Country);
        }

        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    /// <summary>Creates, saves and pays an invoice with one line item and the given tax rate.</summary>
    /// <param name="factory">The host to seed against.</param>
    /// <param name="clientId">The client to bill.</param>
    /// <param name="taxRate">The tax rate to apply; zero for none.</param>
    /// <param name="itemAmount">The single line item's unit price.</param>
    /// <returns>The persisted, paid invoice's id.</returns>
    public static async Task<int> CreatePaidInvoiceAsync(
        IntegrationTestFactory factory, int clientId, decimal taxRate, decimal itemAmount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", itemAmount, 1);
        invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, null, taxRate);
        invoice.RecordPaidHistory($"tx-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    /// <summary>Reads the <c>type</c> field of a refusal answered by <c>BaseApiController</c>.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The error code.</returns>
    public static async Task<string?> CodeOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("type").GetString();
    }
}
