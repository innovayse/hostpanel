using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/credit-notes/{id}</c> against Postgres: staff read one note with its lines; an unknown id is a 404 with its
/// code; clients and anonymous callers are refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetCreditNoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The note comes back with its amounts, its method, its status and its lines.</summary>
    [Fact]
    public async Task Get_ReturnsTheNoteWithItsLines()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m, taxRate: 20m);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 50m);

        var response = await admin.GetAsync($"/api/billing/credit-notes/{noteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var note = body.GetProperty("note");
        note.GetProperty("id").GetInt32().Should().Be(noteId);
        note.GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
        note.GetProperty("clientId").GetInt32().Should().Be(clientId);
        note.GetProperty("status").GetString().Should().Be("Applied");
        note.GetProperty("taxRate").GetDecimal().Should().Be(20m);
        note.GetProperty("total").GetDecimal().Should().Be(60m);
        note.GetProperty("reason").GetString().Should().Be(CreditNoteTestSeeds.Reason);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle();
        items[0].GetProperty("description").GetString().Should().Be("Credit for hosting");
        items[0].GetProperty("amount").GetDecimal().Should().Be(50m);
    }

    /// <summary>An id that names no note answers 404 with <c>CREDIT_NOTE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/credit-notes/{BillingTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
    }

    /// <summary>Reading a note is not for anonymous callers or clients.</summary>
    [Fact]
    public async Task Get_IsStaffOnly()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/billing/credit-notes/1");
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-get");
        var asClient = await client.GetAsync("/api/billing/credit-notes/1");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
