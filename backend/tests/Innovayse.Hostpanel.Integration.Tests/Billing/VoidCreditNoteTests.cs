using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/credit-notes/{id}/void</c> against Postgres: voiding an account credit note takes its value off the
/// balance and off the invoice so it can be credited again, keeps the number on record, is refused once the client has spent the
/// credit, and is refused outright for a note paid out as a gateway refund. A note is never deleted.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class VoidCreditNoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Posts a void with the given reason.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="noteId">The note.</param>
    /// <param name="reason">The reason sent.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> VoidAsync(HttpClient admin, int noteId, string reason = "Issued by mistake") =>
        admin.PostAsJsonAsync($"/api/billing/credit-notes/{noteId}/void", new { reason });

    /// <summary>
    /// Voiding takes the value off the balance with a history line of its own type, frees the invoice's credit, keeps the row with
    /// its number and the reason, and lets the invoice be credited in full again.
    /// </summary>
    [Fact]
    public async Task Void_TakesTheValueBackAndKeepsTheNote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 100m);
        var number = (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Number;

        var response = await VoidAsync(admin, noteId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var note = await CreditNoteTestSeeds.NoteAsync(factory, noteId);
        note.Status.Should().Be(CreditNoteStatus.Voided);
        note.Number.Should().Be(number);
        note.VoidReason.Should().Be("Issued by mistake");
        note.VoidedAt.Should().NotBeNull();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Select(t => (t.CreditMovement, t.CreditChange)).Should().Equal(
            (CreditMovementType.CreditNoteIssued, 100m), (CreditMovementType.CreditNoteVoided, -100m));
        var invoice = await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId);
        invoice.CreditNotedAmount.Should().Be(0m);
        invoice.CreditableAmount.Should().Be(100m);
        (await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 100m)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A note is voided once; the second void is a 409 and moves nothing.</summary>
    [Fact]
    public async Task Void_Twice_IsRefusedTheSecondTime()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 50m), 50m);
        (await VoidAsync(admin, noteId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await VoidAsync(admin, noteId);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(again)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_ALREADY_VOIDED);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>Two voids racing on one note take the value off once.</summary>
    [Fact]
    public async Task Void_Race_TakesTheValueOffOnce()
    {
        var first = await BillingTestSeeds.AdminClientAsync(factory);
        var second = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 500m);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            first, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 50m), 50m);

        var outcomes = await Task.WhenAll(VoidAsync(first, noteId), VoidAsync(second, noteId));

        outcomes.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        outcomes.Single(r => r.StatusCode != HttpStatusCode.NoContent).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(500m);
    }

    /// <summary>A client who has spent the credit the note gave leaves the balance short; the void is refused whole.</summary>
    [Fact]
    public async Task Void_AfterTheClientSpentTheCredit_IsRefusedAndChangesNothing()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 100m);
        var spent = await admin.PostAsJsonAsync($"/api/clients/{clientId}/credit/adjustments", new
        {
            direction = nameof(CreditAdjustment.Deduct),
            amount = 60m,
            reason = "Spent elsewhere",
        });
        spent.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await VoidAsync(admin, noteId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Status.Should().Be(CreditNoteStatus.Applied);
        (await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId)).CreditNotedAmount.Should().Be(100m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(40m);
    }

    /// <summary>A note paid out as a gateway refund is never voided: the refund cannot be taken back.</summary>
    [Fact]
    public async Task Void_OfAGatewayRefundNote_IsRefused()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-void" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 40m, nameof(CreditNoteMethod.GatewayRefund));

        var response = await VoidAsync(admin, noteId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_REFUND_NOT_VOIDABLE);
        (await CreditNoteTestSeeds.NoteAsync(host, noteId)).Status.Should().Be(CreditNoteStatus.Applied);
    }

    /// <summary>An id that names no note answers 404, and a blank reason is refused.</summary>
    [Fact]
    public async Task Void_UnknownNoteAndBlankReason_AreRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 50m), 50m);

        var unknown = await VoidAsync(admin, BillingTestSeeds.NoSuchId);
        var blank = await VoidAsync(admin, noteId, reason: "  ");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(unknown)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        blank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Status.Should().Be(CreditNoteStatus.Applied);
    }

    /// <summary>The API offers no way to delete a note: the route answers 404 or 405, and the row stays.</summary>
    [Fact]
    public async Task ANote_CannotBeDeleted()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var noteId = await CreditNoteTestSeeds.IssueOkAsync(
            admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 50m), 50m);

        var response = await admin.DeleteAsync($"/api/billing/credit-notes/{noteId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await CreditNoteTestSeeds.NoteAsync(factory, noteId)).Number.Should().NotBeNull();
    }
}
