using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/credit</c> against Postgres: a client pays part or all of their own invoice from their
/// balance. The invoice must be theirs, the credit is taken off the balance and put on the invoice in one save, and the
/// move is on the credit history.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApplyMyInvoiceCreditTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Applying part of the balance lowers what the invoice owes and the balance, and records the move.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AppliesPartOfTheBalanceToTheCallersInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-part");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 60m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);

        var response = await owner.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/credit", new { amount = 30m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var invoice = await CreditTestSeeds.InvoiceAsync(factory, invoiceId);
        invoice.Credit.Should().Be(30m);
        invoice.Total.Should().Be(70m);
        invoice.AmountDue.Should().Be(70m);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(30m);
        var line = (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().ContainSingle().Subject;
        line.CreditMovement.Should().Be(CreditMovementType.InvoiceApplied);
        line.CreditChange.Should().Be(-30m);
        line.BalanceAfter.Should().Be(30m);
        line.InvoiceId.Should().Be(invoiceId);
        line.MovesMoney.Should().BeFalse();
    }

    /// <summary>Credit that covers all that is owed settles the invoice in the same save.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreditThatCoversTheInvoice_Settles()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-full");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 50m);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);

        var response = await owner.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/credit", new { amount = 20m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(factory, invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(30m);
    }

    /// <summary>Another client's invoice and an invoice that does not exist are both 404, so ids cannot be enumerated; nothing moves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherClientsInvoice_IsNotFound()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-owner");
        var (_, otherId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-other");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 50m);
        var foreign = await BillingTestSeeds.CreateInvoiceAsync(admin, otherId, 20m);

        var theirs = await owner.PostAsJsonAsync($"/api/me/invoices/{foreign}/credit", new { amount = 10m });
        var missing = await owner.PostAsJsonAsync($"/api/me/invoices/{BillingTestSeeds.NoSuchId}/credit", new { amount = 10m });

        theirs.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(theirs)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CreditTestSeeds.InvoiceAsync(factory, foreign)).Credit.Should().Be(0m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(50m);
    }

    /// <summary>An invoice id in the body is ignored: the route names the invoice, so a body cannot aim the credit elsewhere.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheRouteInvoiceWinsOverTheBody()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-route");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 50m);
        var routed = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);
        var aimed = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 20m);

        var response = await owner.PostAsJsonAsync($"/api/me/invoices/{routed}/credit", new { invoiceId = aimed, amount = 5m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CreditTestSeeds.InvoiceAsync(factory, routed)).Credit.Should().Be(5m);
        (await CreditTestSeeds.InvoiceAsync(factory, aimed)).Credit.Should().Be(0m);
    }

    /// <summary>More than the balance holds is refused with its code; so is more than the invoice owes, and a closed invoice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesWhatTheBalanceOrTheInvoiceCannotTake()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-refuse");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 15m);
        var big = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        var small = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var paid = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        await BillingTestSeeds.MarkPaidAsync(admin, paid, 10m);

        var tooMuchForTheBalance = await owner.PostAsJsonAsync($"/api/me/invoices/{big}/credit", new { amount = 16m });
        var moreThanOwed = await owner.PostAsJsonAsync($"/api/me/invoices/{small}/credit", new { amount = 11m });
        var closed = await owner.PostAsJsonAsync($"/api/me/invoices/{paid}/credit", new { amount = 5m });

        (await BillingTestSeeds.CodeOfAsync(tooMuchForTheBalance)).Should().Be(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        (await BillingTestSeeds.CodeOfAsync(moreThanOwed)).Should().Be(HostpanelErrorCodes.INVOICE_CREDIT_EXCEEDS_DUE);
        (await BillingTestSeeds.CodeOfAsync(closed)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_EDITABLE);
        new[] { tooMuchForTheBalance, moreThanOwed, closed }.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(15m);
        (await CreditTestSeeds.HistoryAsync(factory, clientId)).Should().BeEmpty();
    }

    /// <summary>An amount of nothing is a malformed request.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesANonPositiveAmount()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-zero");
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await owner.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/credit", new { amount = 0m });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Nobody is not a client: the route needs a signed-in one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NeedsASignedInClient()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/invoices/1/credit", new { amount = 1m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Two applications of the same balance to two invoices at once: the balance is never overspent. One lands, the other is
    /// refused (short balance, or the balance moved underneath it), and the history holds exactly the one move.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoApplicationsOfOneBalance_NeverOverspendIt()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, null, "applymine-race");
        await BillingTestSeeds.GiveCreditAsync(factory, clientId, 60m);
        var first = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 60m);
        var second = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 60m);

        var results = await Task.WhenAll(
            owner.PostAsJsonAsync($"/api/me/invoices/{first}/credit", new { amount = 60m }),
            owner.PostAsJsonAsync($"/api/me/invoices/{second}/credit", new { amount = 60m }));

        results.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        results.Single(r => r.StatusCode != HttpStatusCode.NoContent).StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        var history = await CreditTestSeeds.HistoryAsync(factory, clientId);
        history.Should().ContainSingle().Which.CreditChange.Should().Be(-60m);
        var applied = (await CreditTestSeeds.InvoiceAsync(factory, first)).Credit + (await CreditTestSeeds.InvoiceAsync(factory, second)).Credit;
        applied.Should().Be(60m);
    }
}
