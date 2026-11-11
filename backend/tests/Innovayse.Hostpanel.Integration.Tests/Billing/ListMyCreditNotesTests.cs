using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/me/credit-notes</c> against Postgres: a client sees the credit notes issued to them and only those, newest first,
/// never a draft and never another client's note; the route needs a client session.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListMyCreditNotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The caller sees their own notes, newest first, and none of a stranger's or a draft.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsOnlyTheCallersIssuedNotes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-mine");
        var strangerId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m, taxRate: 20m);
        var strangerInvoice = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, strangerId);
        var first = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 10m);
        var second = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 20m);
        await CreditNoteTestSeeds.IssueOkAsync(admin, strangerInvoice, 5m);
        await CreditNoteTestSeeds.DraftAsync(factory, invoiceId);

        var response = await owner.GetAsync("/api/me/credit-notes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        rows.Select(r => r.GetProperty("id").GetInt32()).Should().Equal(second, first);
        rows[0].GetProperty("number").GetString().Should().StartWith("CN-");
        rows[0].GetProperty("total").GetDecimal().Should().Be(24m);
        rows[0].GetProperty("invoiceId").GetInt32().Should().Be(invoiceId);
        rows[0].GetProperty("status").GetString().Should().Be("Applied");
        rows[0].TryGetProperty("clientId", out _).Should().BeFalse("a client is never shown an id of the account list");
    }

    /// <summary>A voided note stays in the client's list, marked voided, so the record is complete.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_KeepsAVoidedNote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId, _, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-mine-void");
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 50m), 50m);
        (await admin.PostAsJsonAsync($"/api/billing/credit-notes/{noteId}/void", new { reason = "Mistake" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var rows = (await (await owner.GetAsync("/api/me/credit-notes")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        rows.Should().ContainSingle();
        rows[0].GetProperty("status").GetString().Should().Be("Voided");
        rows[0].GetProperty("voidedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>A client with no notes gets an empty list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_WithoutNotes_IsEmpty()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-mine-none");

        var response = await owner.GetAsync("/api/me/credit-notes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }

    /// <summary>The route needs a session, and staff use their own route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_NeedsAClientSession()
    {
        var anonymous = await factory.CreateClient().GetAsync("/api/me/credit-notes");
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var asAdmin = await admin.GetAsync("/api/me/credit-notes");

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
