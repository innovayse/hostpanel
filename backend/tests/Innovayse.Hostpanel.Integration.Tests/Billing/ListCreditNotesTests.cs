using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/credit-notes</c> against Postgres: staff list the notes newest first, filtered by invoice, client or status,
/// each row carrying the client name; clients and anonymous callers are refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCreditNotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list filters by invoice and by client, newest first, with the number, the status and the client name.</summary>
    /// <returns>A task that completes when the test has run.</returns>
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

        var byInvoice = await CreditNoteTestSeeds.ListAsync(admin, $"invoiceId={invoiceId}");
        var byClient = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}");

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
    /// <returns>A task that completes when the test has run.</returns>
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

        var onlyVoided = await CreditNoteTestSeeds.ListAsync(admin, $"invoiceId={invoiceId}&status=Voided");
        var onlyApplied = await CreditNoteTestSeeds.ListAsync(admin, $"invoiceId={invoiceId}&status=Applied");

        onlyVoided.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).Should().Equal(voided);
        onlyApplied.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetInt32()).Should().Equal(kept);
    }

    /// <summary>The method filter keeps only the notes given back that way.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_FiltersByMethod()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-list" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);
        var credit = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 10m);
        var refund = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 20m, nameof(CreditNoteMethod.GatewayRefund));

        var onlyRefunds = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&method=GatewayRefund");
        var onlyCredits = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&method=AccountCredit");

        InvoiceListTestSeeds.IdsOf(onlyRefunds).Should().Equal(refund);
        InvoiceListTestSeeds.IdsOf(onlyCredits).Should().Equal(credit);
    }

    /// <summary>The search matches the number, the reason, the refund reference and the id in any letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchMatchesNumberReasonRefundReferenceAndId()
    {
        var plugin = new FakePaymentPlugin { RefundResult = $"gw-ref-{Guid.NewGuid():N}" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);
        var token = Guid.NewGuid().ToString("N");
        var reasoned = await (await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 5m, reason: $"Outage {token}")).Content.ReadFromJsonAsync<int>();
        var refund = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 6m, nameof(CreditNoteMethod.GatewayRefund));
        var number = (await CreditNoteTestSeeds.NoteAsync(host, reasoned)).Number!;

        var byReason = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&search=OUTAGE {token}");
        var byNumber = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&search={number.ToLowerInvariant()}");
        var byReference = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&search={plugin.RefundResult!.ToUpperInvariant()}");
        var byId = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&search={refund}");

        InvoiceListTestSeeds.IdsOf(byReason).Should().Equal(reasoned);
        InvoiceListTestSeeds.IdsOf(byNumber).Should().Equal(reasoned);
        InvoiceListTestSeeds.IdsOf(byReference).Should().Equal(refund);
        InvoiceListTestSeeds.IdsOf(byId).Should().Contain(refund);
    }

    /// <summary>A literal percent sign or underscore in the search matches itself, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SearchTreatsPercentAndUnderscoreAsLiteralText()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var token = Guid.NewGuid().ToString("N");
        var literal = await (await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 5m, reason: $"a%b_{token}")).Content.ReadFromJsonAsync<int>();
        await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 6m, reason: $"aXbY{token}");

        var page = await CreditNoteTestSeeds.ListAsync(admin, $"clientId={clientId}&search={Uri.EscapeDataString($"a%b_{token}")}");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(literal);
    }

    /// <summary>A page holds the newest notes first, the total counts every match and the next page continues where it stopped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_PagesTheNewestFirstOrder()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var first = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 1m);
        var second = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 2m);
        var third = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 3m);

        var pageOne = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/credit-notes?page=1&pageSize=2&clientId={clientId}");
        var pageTwo = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/credit-notes?page=2&pageSize=2&clientId={clientId}");

        InvoiceListTestSeeds.IdsOf(pageOne).Should().Equal(third, second);
        InvoiceListTestSeeds.IdsOf(pageTwo).Should().Equal(first);
        pageOne.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    /// <summary>A value out of range is refused with 400 and names the field, never clamped.</summary>
    /// <param name="queryString">The query string sent.</param>
    /// <param name="field">The field the refusal must name, in lower case.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    [InlineData("page=1&pageSize=20&invoiceId=0", "invoiceid")]
    [InlineData("page=1&pageSize=20&clientId=-3", "clientid")]
    [InlineData("page=1&pageSize=20&status=99", "status")]
    [InlineData("page=1&pageSize=20&method=99", "method")]
    public async Task List_OutOfRangeValue_Returns400NamingTheField(string queryString, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/credit-notes?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InvoiceListTestSeeds.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A status or method that names nothing is refused.</summary>
    /// <param name="filter">The filter sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("status=Sideways")]
    [InlineData("method=Sideways")]
    public async Task List_WithAnUnknownName_Returns400(string filter)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/credit-notes?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The list is not for anonymous callers or clients.</summary>
    /// <returns>A task that completes when the test has run.</returns>
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
