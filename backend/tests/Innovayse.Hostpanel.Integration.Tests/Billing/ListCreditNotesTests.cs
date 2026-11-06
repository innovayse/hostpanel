using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/credit-notes</c> against Postgres: staff list the notes newest first, filtered by invoice, client or status,
/// each row carrying the client name; clients and anonymous callers are refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCreditNotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list filters by invoice and by client, newest first, with the number, the status and the client name.</summary>
    [Fact]
    public async Task List_FiltersByInvoiceAndClient_NewestFirst()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, firstName: "Grace", lastName: "Hopper");
        var otherId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var otherInvoice = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, otherId, price: 100m);
        var first = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 10m);
        var second = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 20m);
        await CreditNoteTestSeeds.IssueOkAsync(admin, otherInvoice, 5m);

        var byInvoice = await CreditNoteTestSeeds.ListAsync(admin, $"?invoiceId={invoiceId}");
        var byClient = await CreditNoteTestSeeds.ListAsync(admin, $"?clientId={clientId}");

        var rows = byInvoice.GetProperty("items").EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("id").GetInt32()).Should().Equal(second, first);
        rows[0].GetProperty("clientName").GetString().Should().Be("Grace Hopper");
        rows[0].GetProperty("status").GetString().Should().Be("Applied");
        rows[0].GetProperty("method").GetString().Should().Be("AccountCredit");
        rows[0].GetProperty("number").GetString().Should().StartWith("CN-");
        rows[0].GetProperty("total").GetDecimal().Should().Be(20m);
        byClient.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).Should().Equal(second, first);
    }

    /// <summary>The status filter keeps only the notes in that state.</summary>
    [Fact]
    public async Task List_FiltersByStatus()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var kept = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 10m);
        var voided = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 20m);
        (await admin.PostAsync($"/api/billing/credit-notes/{voided}/void", JsonContent.Create(new { reason = "Mistake" }))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var onlyVoided = await CreditNoteTestSeeds.ListAsync(admin, $"?invoiceId={invoiceId}&status=Voided");
        var onlyApplied = await CreditNoteTestSeeds.ListAsync(admin, $"?invoiceId={invoiceId}&status=Applied");

        onlyVoided.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).Should().Equal(voided);
        onlyApplied.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).Should().Equal(kept);
    }

    /// <summary>A malformed filter is refused.</summary>
    [Fact]
    public async Task List_WithAnUnknownStatus_Returns400()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/billing/credit-notes?status=Sideways");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The list is not for anonymous callers or clients.</summary>
    [Fact]
    public async Task List_IsStaffOnly()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/billing/credit-notes");
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-list");
        var asClient = await client.GetAsync("/api/billing/credit-notes");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
