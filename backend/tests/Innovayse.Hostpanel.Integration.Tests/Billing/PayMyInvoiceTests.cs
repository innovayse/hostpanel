using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/pay</c> against Postgres, with the card gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: a client pays their own invoice with a payment method
/// saved on their account, and an invoice is marked paid only when the gateway reports the money
/// taken. Foreign or invented method ids, other clients' invoices and a deployment without a card
/// gateway are all refused before any charge is attempted.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PayMyInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved payment method every seeded customer owns in these tests.</summary>
    private const string SavedMethodId = "pm_saved_1";

    /// <summary>The card gateway customer id stamped on the paying client.</summary>
    private const string CustomerId = "cus_pay_1";

    /// <summary>Builds a configured fake card gateway that lists <see cref="SavedMethodId"/> for the customer.</summary>
    /// <returns>The fake gateway.</returns>
    private static FakeCardPaymentGateway ConfiguredGateway() => new()
    {
        IsConfigured = true,
        SavedPaymentMethods = [new CardPaymentMethodDto(SavedMethodId, SavedPaymentMethodType.Card, "visa", "4242", 12, 2035, true)],
    };

    /// <summary>Builds a host whose card gateway is <paramref name="cardGateway"/>.</summary>
    /// <param name="cardGateway">The fake card gateway to serve every call through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway cardGateway) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));

    /// <summary>Registers a client with a card customer on file and raises an unpaid invoice for them.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="label">Distinguishes the seeded email between tests.</param>
    /// <returns>The client's authenticated caller, the admin caller and the invoice id.</returns>
    private async Task<OwnedInvoice> SeedAsync(
        WebApplicationFactory<Program> host, string label)
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, label);
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, CustomerId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        return new(owner, admin, invoiceId);
    }

    /// <summary>Pays an invoice with the given payment method id, as a new pay action.</summary>
    /// <param name="caller">The paying client.</param>
    /// <param name="invoiceId">The invoice to pay.</param>
    /// <param name="paymentMethodId">The payment method to charge.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PayAsync(HttpClient caller, int invoiceId, string? paymentMethodId) =>
        PayAsync(caller, invoiceId, paymentMethodId, Guid.NewGuid());

    /// <summary>Pays an invoice with the given payment method id under the given payment attempt.</summary>
    /// <param name="caller">The paying client.</param>
    /// <param name="invoiceId">The invoice to pay.</param>
    /// <param name="paymentMethodId">The payment method to charge.</param>
    /// <param name="attemptId">The payment attempt; the same value is a retry of the same pay action.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PayAsync(HttpClient caller, int invoiceId, string? paymentMethodId, Guid? attemptId) =>
        caller.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/pay", new { paymentMethodId, attemptId });

    /// <summary>Reads an invoice's status as the admin sees it.</summary>
    /// <param name="admin">The admin caller.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status name.</returns>
    private static async Task<string?> StatusOfAsync(HttpClient admin, int invoiceId)
    {
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("status").GetString();
    }

    /// <summary>Counts the ledger rows written against an invoice.</summary>
    /// <param name="host">The test host whose database to read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The number of ledger rows.</returns>
    private static async Task<int> LedgerRowsAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().CountAsync(t => t.InvoiceId == invoiceId);
    }

    /// <summary>
    /// The owner pays with a saved method: the gateway is charged that method for the invoice's total
    /// and currency, and only then is the invoice marked paid with exactly one ledger row carrying the
    /// gateway's reference.
    /// </summary>
    [Fact]
    public async Task Pay_WithASavedMethod_ChargesTheGatewayThenMarksItPaid()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-pay");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.LastChargeInstruction.Should().NotBeNull();
        cardGateway.LastChargeInstruction!.CustomerId.Should().Be(CustomerId);
        cardGateway.LastChargeInstruction.PaymentMethodId.Should().Be(SavedMethodId);
        cardGateway.LastChargeInstruction.Amount.Should().Be(10m);
        cardGateway.LastChargeInstruction.InvoiceId.Should().Be(invoiceId);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
    }

    /// <summary>A declined charge is refused with 502 and leaves the invoice unpaid, with no ledger row.</summary>
    [Fact]
    public async Task Pay_WhenTheGatewayDeclines_RefusesAndLeavesItUnpaid()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.Charge = new CardChargeResult(false, null);
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-declined");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(0);
    }

    /// <summary>
    /// A gateway that cannot be reached is refused with 503 and <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>,
    /// never a 500, and leaves the invoice unpaid with no ledger row.
    /// </summary>
    [Fact]
    public async Task Pay_WhenTheGatewayIsUnavailable_AnswersServiceUnavailableAndLeavesItUnpaid()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.Charge = new CardChargeResult(false, null, CardGatewayFailure.Unavailable);
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-unavailable");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(0);
    }

    /// <summary>A success with no transaction id is refused with 502 rather than recorded with nothing to reconcile.</summary>
    [Fact]
    public async Task Pay_WhenTheGatewayReportsSuccessWithoutAReference_RefusesAndLeavesItUnpaid()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.Charge = new CardChargeResult(true, null);
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-noref");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// A method id that is not saved on the caller's account (another customer's, or invented) is
    /// refused with <c>PAYMENT_METHOD_NOT_OWNED</c> and the gateway is never charged.
    /// </summary>
    [Fact]
    public async Task Pay_WithAForeignOrUnknownMethodId_RefusesWithoutChargingAnything()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-foreign-method");

        var response = await PayAsync(owner, invoiceId, "pm_someone_elses");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>An account that has never saved a method is refused with <c>NO_SAVED_PAYMENT_METHODS</c> and nothing is charged.</summary>
    [Fact]
    public async Task Pay_WithoutACardCustomer_RefusesWithNoSavedPaymentMethods()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "owner-no-customer");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// A deployment without a card gateway refuses with 409 <c>PAYMENT_METHOD_UNAVAILABLE</c>: the
    /// invoice is never marked paid by a payment path that cannot take money.
    /// </summary>
    [Fact]
    public async Task Pay_WhenNoCardGatewayIsConfigured_RefusesAndLeavesItUnpaid()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.IsConfigured = false;
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-unconfigured");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>An already-paid invoice is refused with <c>INVOICE_NOT_PAYABLE</c> before the gateway is charged a second time.</summary>
    [Fact]
    public async Task Pay_AnAlreadyPaidInvoice_RefusesWithoutChargingAgain()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-paid-twice");
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        cardGateway.LastChargeInstruction.Should().BeNull();
    }

    /// <summary>A body with no payment method id is refused by validation, not guessed at.</summary>
    [Fact]
    public async Task Pay_WithoutAMethodId_RefusesWithAValidationError()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-no-method-id");

        var response = await PayAsync(owner, invoiceId, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// Paying a stranger's invoice is refused with <c>INVOICE_NOT_FOUND</c> before the gateway is
    /// reached, so the invoice stays unpaid.
    /// </summary>
    [Fact]
    public async Task Pay_AStrangersInvoice_Returns404WithInvoiceNotFoundAndChargesNothing()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (_, admin, invoiceId) = await SeedAsync(host, "owner-strangers-pay");
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, host, "stranger-pay");

        var response = await PayAsync(stranger, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>Paying an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task Pay_AnUnknownInvoice_Returns404WithInvoiceNotFound()
    {
        var (owner, _) = await BillingTestSeeds.RegisterClientAsync(factory, null, "owner-unknown-pay");

        var response = await PayAsync(owner, BillingTestSeeds.NoSuchId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>
    /// In the demo the same endpoint settles through the demo card gateway behind the same port: the
    /// sample method listed for the customer is charged, and a method id of another customer is refused.
    /// </summary>
    [Fact]
    public async Task Pay_InTheDemo_SettlesThroughTheDemoCardGateway()
    {
        var host = DemoTestHost.Enabled(factory);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-demo-pay");

        var foreign = await PayAsync(owner, invoiceId, "demo-card-visa:cus_someone_else");
        (await BillingTestSeeds.CodeOfAsync(foreign)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));

        var response = await PayAsync(owner, invoiceId, $"demo-card-visa:{CustomerId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
    }

    /// <summary>Paying an invoice is not for anonymous callers.</summary>
    [Fact]
    public async Task PayRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/me/invoices/1/pay", new { paymentMethodId = SavedMethodId });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A body with no payment attempt is refused by validation before any charge.</summary>
    [Fact]
    public async Task Pay_WithoutAnAttemptId_RefusesWithAValidationError()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-no-attempt-id");

        var response = await PayAsync(owner, invoiceId, SavedMethodId, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// The same payment attempt sent twice is one charge at the gateway: both requests carry the
    /// same idempotency key, built from the invoice and the attempt, so the gateway takes the money
    /// once.
    /// </summary>
    [Fact]
    public async Task Pay_SameAttemptTwice_ChargesOnce()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.Charge = new CardChargeResult(false, null);
        var host = BuildHost(cardGateway);
        var (owner, _, invoiceId) = await SeedAsync(host, "owner-idempotent");
        var attemptId = Guid.NewGuid();

        await PayAsync(owner, invoiceId, SavedMethodId, attemptId);
        await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        cardGateway.ChargesByKey.Should().ContainSingle()
            .Which.Key.Should().Be($"invoice-{invoiceId}-attempt-{attemptId:N}");
    }

    /// <summary>
    /// After a decline the same card is charged again at once by a new pay action: its new attempt
    /// is a new idempotency key, so the gateway does not replay the decline, and the invoice is paid.
    /// </summary>
    [Fact]
    public async Task Pay_NewAttemptAfterADecline_ChargesAgain()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.Charge = new CardChargeResult(false, null);
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-retry-after-decline");
        var declinedAttempt = Guid.NewGuid();

        var declined = await PayAsync(owner, invoiceId, SavedMethodId, declinedAttempt);
        cardGateway.Charge = new CardChargeResult(true, $"txn-retry-{Guid.NewGuid():N}");
        var replayed = await PayAsync(owner, invoiceId, SavedMethodId, declinedAttempt);
        var retried = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid());

        declined.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        replayed.StatusCode.Should().Be(HttpStatusCode.BadGateway, "the gateway replays the decline for the same attempt");
        retried.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.ChargesByKey.Should().HaveCount(2);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
    }

    /// <summary>
    /// A double click of one pay action: both requests carry the same attempt and reach the gateway,
    /// which answers the second with the first charge (same idempotency key, same transaction). The
    /// invoice is paid once, with one ledger row, and nothing is refunded; neither request fails with
    /// an unhandled conflict.
    /// </summary>
    [Fact]
    public async Task Pay_DoubleClick_RecordsOnePaymentAndRefundsNothing()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-double-click");
        var attemptId = Guid.NewGuid();

        var responses = await Task.WhenAll(
            PayAsync(owner, invoiceId, SavedMethodId, attemptId), PayAsync(owner, invoiceId, SavedMethodId, attemptId));

        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.NoContent);
        foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.NoContent))
        {
            // Depending on whether the slower click read the invoice before or after the faster one
            // saved, it was refused either before charge with INVOICE_NOT_PAYABLE or at settlement
            // with PAYMENT_INTENT_MISMATCH. Both indicate a safely lost race with no duplicate payment.
            (await BillingTestSeeds.CodeOfAsync(response)).Should().BeOneOf(
                HostpanelErrorCodes.INVOICE_NOT_PAYABLE,
                HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        }

        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
        cardGateway.RefundInstructions.Should().BeEmpty();
        cardGateway.ChargesByKey.Should().HaveCountLessThanOrEqualTo(1);
    }

    /// <summary>
    /// A retry of the attempt that already paid the invoice (its answer was lost) answers success:
    /// the gateway's charge is found by the attempt's key without charging again, and the settlement
    /// replays. The invoice keeps one ledger row and nothing is refunded.
    /// </summary>
    [Fact]
    public async Task Pay_SameAttemptAfterItSettled_AnswersSuccessWithoutChargingAgain()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-retry-after-success");
        var attemptId = Guid.NewGuid();

        var first = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);
        var retried = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        retried.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.ChargesByKey.Should().ContainSingle();
        cardGateway.ChargeLookups.Should().Be(1);
        cardGateway.RefundInstructions.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
    }

    /// <summary>
    /// A new attempt on an invoice another attempt already paid finds no charge of its own at the
    /// gateway and is refused with <c>INVOICE_NOT_PAYABLE</c>, without charging anything.
    /// </summary>
    [Fact]
    public async Task Pay_NewAttemptAfterAnotherSettledIt_RefusesWithoutCharging()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, _, invoiceId) = await SeedAsync(host, "owner-new-attempt-after-success");

        var first = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid());
        var second = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid());

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CodeOfAsync(second)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        cardGateway.ChargesByKey.Should().ContainSingle();
        cardGateway.ChargeLookups.Should().Be(1);
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
    }

    /// <summary>
    /// A card gateway that cannot list the account's methods answers 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>, never a 500, and charges nothing.
    /// </summary>
    [Fact]
    public async Task Pay_WhenTheGatewayCannotListMethods_AnswersServiceUnavailableWithoutCharging()
    {
        var cardGateway = ConfiguredGateway();
        cardGateway.ListFailure = CardGatewayFailure.Unavailable;
        var host = BuildHost(cardGateway);
        var (owner, admin, invoiceId) = await SeedAsync(host, "owner-list-unavailable");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        cardGateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// A retry of a settled attempt whose charge lookup the gateway refuses answers 502 with
    /// <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> rather than claiming nothing was taken, and charges nothing again.
    /// </summary>
    [Fact]
    public async Task Pay_SameAttemptAfterItSettled_WhenTheLookupIsRefused_AnswersBadGateway()
    {
        var cardGateway = ConfiguredGateway();
        var host = BuildHost(cardGateway);
        var (owner, _, invoiceId) = await SeedAsync(host, "owner-lookup-refused");
        var attemptId = Guid.NewGuid();

        var first = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);
        cardGateway.LookupFailure = CardGatewayFailure.Refused;
        var retried = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        retried.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(retried)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UPSTREAM_FAILED);
        cardGateway.ChargesByKey.Should().ContainSingle();
        (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
    }
}
