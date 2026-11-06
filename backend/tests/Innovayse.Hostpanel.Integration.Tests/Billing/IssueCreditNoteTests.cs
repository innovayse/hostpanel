using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/credit-notes</c> against Postgres: a note is numbered from its own sequence, follows the invoice's tax
/// rate, is capped by what the invoice can still be credited (earlier notes and refunds counted, also under a race), moves its
/// value once by the one mechanism (account credit or a gateway refund), and is refused on an unpaid invoice and on a top-up.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed partial class IssueCreditNoteTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A credit note number: the prefix and at least four digits.</summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"^CN-\d{4,}$")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// A full account credit note of a taxed invoice reaches the balance: the note is Applied with its number, the tax follows the
    /// invoice at the same rate, the balance rises by the note's total with a history line of its own type, and the invoice stays
    /// Paid with its credit noted.
    /// </summary>
    [Fact]
    public async Task AccountCredit_FullNote_PutsTheTotalOnTheBalanceAndTheHistory()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m, taxRate: 20m);

        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 100m);

        var note = await CreditNoteTestSeeds.NoteAsync(factory, noteId);
        note.Number.Should().MatchRegex(NumberPattern().ToString());
        note.Status.Should().Be(CreditNoteStatus.Applied);
        note.Method.Should().Be(CreditNoteMethod.AccountCredit);
        note.ClientId.Should().Be(clientId);
        note.TaxRate.Should().Be(20m);
        (note.SubTotal, note.Tax, note.Total).Should().Be((100m, 20m, 120m));
        note.Items.Should().ContainSingle().Which.Amount.Should().Be(100m);
        note.IssuedAt.Should().NotBeNull();
        note.AppliedAt.Should().NotBeNull();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(120m);
        var line = (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.CreditNoteIssued);
        line.CreditChange.Should().Be(120m);
        line.BalanceAfter.Should().Be(120m);
        line.InvoiceId.Should().Be(invoiceId);
        line.Description.Should().Contain(note.Number!);
        var invoice = await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.CreditNotedAmount.Should().Be(120m);
        invoice.AmountRefunded.Should().Be(0m);
    }

    /// <summary>A partial note is taxed at the invoice's rate and leaves the rest of the invoice creditable.</summary>
    [Fact]
    public async Task AccountCredit_PartialNote_FollowsTheInvoiceRate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m, taxRate: 20m);

        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 40m);

        var note = await CreditNoteTestSeeds.NoteAsync(factory, noteId);
        (note.SubTotal, note.Tax, note.Total).Should().Be((40m, 8m, 48m));
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(48m);
        (await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId)).CreditableAmount.Should().Be(72m);
    }

    /// <summary>Notes get distinct numbers in order from the one sequence, across invoices.</summary>
    [Fact]
    public async Task Numbers_AreDistinctAndRise()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var first = await CreditNoteTestSeeds.IssueOkAsync(admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId), 10m);
        var second = await CreditNoteTestSeeds.IssueOkAsync(admin, await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId), 10m);

        var firstNumber = (await CreditNoteTestSeeds.NoteAsync(factory, first)).Number!;
        var secondNumber = (await CreditNoteTestSeeds.NoteAsync(factory, second)).Number!;

        secondNumber.Should().NotBe(firstNumber);
        long.Parse(secondNumber[CreditNote.NumberPrefix.Length..]).Should().BeGreaterThan(long.Parse(firstNumber[CreditNote.NumberPrefix.Length..]));
    }

    /// <summary>A note above the invoice's charge is refused with its code and changes nothing.</summary>
    [Fact]
    public async Task OverCredit_AboveTheCharge_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 100.01m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_EXCEEDS_CREDITABLE);
        (await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId)).Should().BeEmpty();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>Earlier notes count: once 70 of 100 is credited, 30 more is allowed and a cent beyond is refused.</summary>
    [Fact]
    public async Task OverCredit_CountsEarlierNotes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 70m);

        var over = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 30.01m);
        var exact = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 30m);

        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(over)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_EXCEEDS_CREDITABLE);
        exact.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(100m);
        (await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId)).CreditableAmount.Should().Be(0m);
    }

    /// <summary>Earlier refunds count too: after a manual refund of 30, only 70 of the invoice can be credited.</summary>
    [Fact]
    public async Task OverCredit_CountsEarlierRefunds()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var refunded = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 30m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.Manual,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });
        refunded.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var over = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 70.01m);
        var exact = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 70m);

        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        exact.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// A refund made on its own creates no note, and a refund after an account credit note is held to what the note left, so
    /// the same money is never given back twice.
    /// </summary>
    [Fact]
    public async Task ARefundAlone_CreatesNoNote_AndAfterANoteIsHeldToTheRest()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);
        var alone = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.Manual,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });
        alone.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId)).Should().BeEmpty();

        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 60m);
        var tooMuch = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 30.01m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.Manual,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });

        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(tooMuch)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
    }

    /// <summary>
    /// Two notes racing for the same invoice cannot both pass the cap: of two notes of 60 on an invoice of 100 exactly one is
    /// saved, the other is refused, and the balance and the invoice agree on 60.
    /// </summary>
    [Fact]
    public async Task Race_TwoNotesAboveTheCapTogether_OnlyOneIsSaved()
    {
        var first = await BillingTestSeeds.AdminClientAsync(factory);
        var second = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);

        var outcomes = await Task.WhenAll(
            CreditNoteTestSeeds.IssueAsync(first, invoiceId, 60m),
            CreditNoteTestSeeds.IssueAsync(second, invoiceId, 60m));

        outcomes.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        outcomes.Single(r => r.StatusCode != HttpStatusCode.Created).StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        (await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId)).Should().ContainSingle();
        (await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId)).CreditNotedAmount.Should().Be(60m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(60m);
    }

    /// <summary>
    /// Two notes that together fit are both saved even when they race: the loser of the row is retried on what the winner saved,
    /// under the same number, and is not refused.
    /// </summary>
    [Fact]
    public async Task Race_TwoNotesThatFitTogether_AreBothSaved()
    {
        var first = await BillingTestSeeds.AdminClientAsync(factory);
        var second = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);

        var outcomes = await Task.WhenAll(
            CreditNoteTestSeeds.IssueAsync(first, invoiceId, 40m),
            CreditNoteTestSeeds.IssueAsync(second, invoiceId, 40m));

        outcomes.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var notes = await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId);
        notes.Should().HaveCount(2);
        notes.Select(n => n.Number).Distinct().Should().HaveCount(2);
        (await CreditNoteTestSeeds.InvoiceAsync(factory, invoiceId)).CreditNotedAmount.Should().Be(80m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(80m);
    }

    /// <summary>A credit top-up is credit already: a note on it is refused with its own code.</summary>
    [Fact]
    public async Task TopUpInvoice_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidTopUpAsync(factory, clientId);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 10m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_TOP_UP_INVOICE);
        (await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId)).Should().BeEmpty();
    }

    /// <summary>An invoice that is not paid takes no credit note.</summary>
    [Fact]
    public async Task UnpaidInvoice_IsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.UnpaidInvoiceAsync(factory, clientId);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 10m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAID);
    }

    /// <summary>An id that names no invoice answers 404.</summary>
    [Fact]
    public async Task UnknownInvoice_Returns404()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, BillingTestSeeds.NoSuchId, 10m);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>A note without lines, without a reason, with a blank line or an unknown method is refused before anything is saved.</summary>
    /// <param name="body">Which malformed body to send.</param>
    [Theory]
    [InlineData("noItems")]
    [InlineData("noReason")]
    [InlineData("freeLine")]
    [InlineData("zeroQuantity")]
    [InlineData("badMethod")]
    public async Task MalformedBody_IsRefused(string body)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId);
        var line = new { description = "Credit", unitPrice = 10m, quantity = 1 };
        object payload = body switch
        {
            "noItems" => new { items = Array.Empty<object>(), method = "AccountCredit", reason = "Because" },
            "noReason" => new { items = new[] { line }, method = "AccountCredit", reason = " " },
            "freeLine" => new { items = new[] { line with { unitPrice = 0m } }, method = "AccountCredit", reason = "Because" },
            "zeroQuantity" => new { items = new[] { line with { quantity = 0 } }, method = "AccountCredit", reason = "Because" },
            _ => new { items = new[] { line }, method = "Sideways", reason = "Because" },
        };

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/credit-notes", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId)).Should().BeEmpty();
    }

    /// <summary>A credit note is not for anonymous callers, and not for a client.</summary>
    [Fact]
    public async Task NotForAnonymousOrClients()
    {
        var anonymous = await factory.CreateClient().PostAsJsonAsync("/api/billing/1/credit-notes", new { });
        var (client, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "cn-issue");
        var asClient = await client.PostAsJsonAsync("/api/billing/1/credit-notes", new { });

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        asClient.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A gateway refund note goes through the one refund of the invoice: the gateway is asked once for the note's total, the
    /// invoice has one refund row and the ledger one refund line, the balance is untouched, and the note records the refund.
    /// </summary>
    [Fact]
    public async Task GatewayRefund_RefundsOnceAndTheBalanceIsUntouched()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-note" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);

        var noteId = await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 40m, nameof(CreditNoteMethod.GatewayRefund));

        plugin.RefundInstructions.Should().ContainSingle().Which.AmountMinor.Should().Be(4000);
        var note = await CreditNoteTestSeeds.NoteAsync(host, noteId);
        note.Status.Should().Be(CreditNoteStatus.Applied);
        note.Method.Should().Be(CreditNoteMethod.GatewayRefund);
        note.RefundReference.Should().Be("gw-refund-note");
        note.Number.Should().MatchRegex(NumberPattern().ToString());
        var invoice = await CreditNoteTestSeeds.InvoiceAsync(host, invoiceId);
        invoice.Transactions.Where(t => t.Type == InvoiceTransactionType.Refund).Should().ContainSingle().Which.Amount.Should().Be(40m);
        invoice.CreditNotedAmount.Should().Be(0m);
        invoice.Status.Should().Be(InvoiceStatus.PartiallyRefunded);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(0m);
        (await CreditTestSeeds.HistoryAsync(host, clientId)).Should().BeEmpty();
        (await CreditTestSeeds.LedgerAsync(host, clientId)).Count(t => t.AmountOut == 40m && t.Source == TransactionSource.GatewayRefund).Should().Be(1);
    }

    /// <summary>A gateway that refuses the refund creates no note and takes no number's worth of state: nothing is saved.</summary>
    [Fact]
    public async Task GatewayRefund_RefusedByTheGateway_CreatesNoNote()
    {
        var plugin = new FakePaymentPlugin { RefundResult = null };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 40m, nameof(CreditNoteMethod.GatewayRefund));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_REFUND_REFUSED);
        (await CreditNoteTestSeeds.NotesOfAsync(host, invoiceId)).Should().BeEmpty();
        (await CreditNoteTestSeeds.InvoiceAsync(host, invoiceId)).AmountRefunded.Should().Be(0m);
    }

    /// <summary>An invoice no gateway took money for cannot be refunded through one: no note, no refund.</summary>
    [Fact]
    public async Task GatewayRefund_OfAManualPayment_IsRefusedWithoutANote()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "never" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(host, clientId);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 40m, nameof(CreditNoteMethod.GatewayRefund));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_GATEWAY_PAID);
        plugin.RefundInstructions.Should().BeEmpty();
        (await CreditNoteTestSeeds.NotesOfAsync(host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>A gateway refund note is held to the cash the gateway can still return, after an account credit note took part.</summary>
    [Fact]
    public async Task GatewayRefund_AfterAnAccountCreditNote_IsHeldToTheRest()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-rest" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);
        await CreditNoteTestSeeds.IssueOkAsync(admin, invoiceId, 70m);

        var over = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 30.01m, nameof(CreditNoteMethod.GatewayRefund));
        var exact = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 30m, nameof(CreditNoteMethod.GatewayRefund));

        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        exact.StatusCode.Should().Be(HttpStatusCode.Created);
        plugin.RefundInstructions.Should().ContainSingle().Which.AmountMinor.Should().Be(3000);
        (await CreditNoteTestSeeds.InvoiceAsync(host, invoiceId)).RefundableAmount.Should().Be(0m);
    }

    /// <summary>
    /// A gateway refund that is claimed and not recorded yet holds the invoice: an account credit note for the same money is refused
    /// with 409 <c>INVOICE_REFUND_CONFLICT</c>, no number is taken and the balance is untouched.
    /// </summary>
    [Fact]
    public async Task AccountCredit_WhileAGatewayRefundIsInFlight_IsRefusedAsAConflict()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-unused" };
        var host = CreditNoteTestSeeds.HostWithGateway(factory, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var invoiceId = await CreditNoteTestSeeds.GatewayPaidInvoiceAsync(host, clientId, price: 100m);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            invoice.BeginGatewayRefund(DateTimeOffset.UtcNow, 40m);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 10m);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_CONFLICT);
        (await CreditNoteTestSeeds.NotesOfAsync(host, invoiceId)).Should().BeEmpty();
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(0m);
        (await CreditNoteTestSeeds.InvoiceAsync(host, invoiceId)).CreditNotedAmount.Should().Be(0m);
    }

    /// <summary>
    /// The balance has no currency of its own, so account credit for an invoice in another currency than the client's balance is
    /// refused with its code before a number is taken, and changes nothing; the same invoice can still be refunded through the gateway.
    /// </summary>
    [Fact]
    public async Task AccountCredit_InAnotherCurrencyThanTheBalance_IsRefused()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "AMD", "051", 0.0026m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, currency: "AMD");
        var invoiceId = await CreditNoteTestSeeds.PaidInvoiceAsync(factory, clientId, price: 100m);

        var response = await CreditNoteTestSeeds.IssueAsync(admin, invoiceId, 10m);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CREDIT_NOTE_CURRENCY_MISMATCH);
        (await CreditNoteTestSeeds.NotesOfAsync(factory, invoiceId)).Should().BeEmpty();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }
}
