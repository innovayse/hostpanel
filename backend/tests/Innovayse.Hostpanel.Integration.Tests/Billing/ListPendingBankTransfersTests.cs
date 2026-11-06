using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/bank-transfers/pending</c> against Postgres: staff see exactly the invoices whose client chose a
/// manual bank transfer and that wait for a decision.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPendingBankTransfersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Reads the list as the admin and returns its rows.</summary>
    /// <param name="admin">The authenticated admin client.</param>
    /// <returns>The rows of the first page.</returns>
    private static async Task<List<JsonElement>> RowsAsync(HttpClient admin)
    {
        var response = await admin.GetAsync("/api/billing/bank-transfers/pending?page=1&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>
    /// A waiting transfer is listed with its reference, client and amount; an unpaid invoice, a confirmed one and a rejected one
    /// are not.
    /// </summary>
    [Fact]
    public async Task List_ShowsOnlyTheTransfersThatWait()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        await BankTransferTestSeeds.EnableAsync(admin);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-list");
        var waiting = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 80m);
        var confirmed = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 81m);
        var rejected = await BankTransferTestSeeds.PendingInvoiceAsync(admin, owner, clientId, 82m);
        var unpaid = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 83m);
        await admin.PostAsync($"/api/billing/{confirmed}/bank-transfer/confirm", content: null);
        await admin.PostAsJsonAsync($"/api/billing/{rejected}/bank-transfer/reject", new { note = "Not received." });

        var rows = await RowsAsync(admin);

        var ids = rows.Select(r => r.GetProperty("invoiceId").GetInt32()).ToList();
        ids.Should().Contain(waiting);
        ids.Should().NotContain([confirmed, rejected, unpaid]);
        var row = rows.Single(r => r.GetProperty("invoiceId").GetInt32() == waiting);
        row.GetProperty("reference").GetString().Should().Be($"INV-{waiting}");
        row.GetProperty("clientId").GetInt32().Should().Be(clientId);
        row.GetProperty("clientName").GetString().Should().NotBeNullOrWhiteSpace();
        row.GetProperty("amountDue").GetDecimal().Should().Be(80m);
        row.GetProperty("currency").GetString().Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Staff only: anonymous callers get 401, and a client account gets 403.</summary>
    [Fact]
    public async Task List_IsForStaffOnly()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/billing/bank-transfers/pending");
        var (client, _, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "bt-list-client");

        var asClient = await client.GetAsync("/api/billing/bank-transfers/pending");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
