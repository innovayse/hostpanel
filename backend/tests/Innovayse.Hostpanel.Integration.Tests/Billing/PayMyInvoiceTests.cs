using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/me/invoices/{id}/pay</c> against Postgres, with the saved-method capability replaced by
/// <see cref="FakeStoredCredentialPlugin"/>: a client pays their own invoice with a payment method saved on their
/// account, and an invoice is marked paid only when the gateway reports the money taken. Foreign or invented method
/// ids, other clients' invoices and a module that is not offered are all refused before any charge is attempted.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PayMyInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved payment method every seeded customer owns in these tests.</summary>
    private const string SavedMethodId = "pm_saved_1";

    /// <summary>The gateway customer reference linked to the paying client, unique per test since a customer belongs to one client only.</summary>
    private readonly string _customerId = $"cus_pay_{Guid.NewGuid():N}"[..24];

    /// <summary>Builds a host whose saved-method capability lists <see cref="SavedMethodId"/> for the customer.</summary>
    /// <param name="interceptors">Interceptors every data context of the host is built with, to hold a request at a chosen point.</param>
    /// <returns>The host with its fake capability.</returns>
    private StoredCredentialHost BuildHost(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var gateway = StoredCredentialHost.Create(factory, interceptors);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(SavedMethodId));
        return gateway;
    }

    /// <summary>Registers a client with a gateway customer on file and raises an unpaid invoice for them.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="label">Distinguishes the seeded email between tests.</param>
    /// <returns>The client's authenticated caller, the admin caller and the invoice id.</returns>
    private async Task<OwnedInvoice> SeedAsync(StoredCredentialHost host, string label)
    {
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host.Host, label);
        await host.LinkAsync(clientId, _customerId);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host.Host);
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
    /// <param name="module">The gateway module the method belongs to.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PayAsync(
        HttpClient caller, int invoiceId, string? paymentMethodId, Guid? attemptId, string? module = StoredCredentialHost.Module) =>
        caller.PostAsJsonAsync($"/api/me/invoices/{invoiceId}/pay", new { gatewayModuleId = module, paymentMethodId, attemptId });

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

    /// <summary>Reads the payment claims of an invoice.</summary>
    /// <param name="host">The test host whose database to read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The claims, oldest first.</returns>
    private static async Task<List<InvoicePaymentClaim>> ClaimsOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoicePaymentClaims.AsNoTracking().Where(c => c.InvoiceId == invoiceId).OrderBy(c => c.Id).ToListAsync();
    }

    /// <summary>
    /// The owner pays with a saved method: the gateway is charged that method for the invoice's total and currency
    /// under the attempt's key, and only then is the invoice marked paid with exactly one ledger row carrying the
    /// gateway's reference and the claim closed as settled.
    /// </summary>
    [Fact]
    public async Task Pay_WithASavedMethod_ChargesTheGatewayThenMarksItPaid()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-pay");
        var attemptId = Guid.NewGuid();

        var response = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var charge = gateway.Plugin.Charges.Should().ContainSingle().Subject;
        charge.Key.Should().Be($"invoice-{invoiceId}-attempt-{attemptId:N}");
        charge.Value.Instruction.CustomerRef.Should().Be(_customerId);
        charge.Value.Instruction.MethodRef.Should().Be(SavedMethodId);
        charge.Value.Instruction.AmountMinor.Should().Be(1000);
        charge.Value.Instruction.Currency.Should().NotBeNullOrEmpty();
        charge.Value.Instruction.InvoiceReference.Should().Be(invoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(1);
        var claim = (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Subject;
        claim.Outcome.Should().Be(InvoicePaymentClaimOutcome.Settled);
        claim.Kind.Should().Be(InvoicePaymentClaimKind.Manual);
        claim.GatewayModuleId.Should().Be(StoredCredentialHost.Module);
    }

    /// <summary>A declined charge is refused with 502, leaves the invoice unpaid with no ledger row, and closes the claim as failed.</summary>
    [Fact]
    public async Task Pay_WhenTheGatewayDeclines_RefusesAndLeavesItUnpaid()
    {
        var gateway = BuildHost();
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-declined");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(0);
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Failed);
    }

    /// <summary>
    /// A gateway that cannot be reached is refused with 503 and <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>, never a
    /// 500, and leaves the invoice unpaid with no ledger row. The claim stays held, since nothing is known about the money.
    /// </summary>
    [Fact]
    public async Task Pay_WhenTheGatewayIsUnavailable_AnswersServiceUnavailableAndHoldsTheClaim()
    {
        var gateway = BuildHost();
        gateway.Plugin.ThrowsBeforeCharging = true;
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-unavailable");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(0);
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>A success with no transaction id is refused with 502 rather than recorded with nothing to reconcile; the claim stays held.</summary>
    [Fact]
    public async Task Pay_WhenTheGatewayReportsSuccessWithoutAReference_RefusesAndHoldsTheClaim()
    {
        var gateway = BuildHost();
        gateway.Plugin.OnCharge = i => new StoredChargeResult(StoredChargeOutcome.Succeeded, null, i.AmountMinor, i.Currency, null);
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-noref");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>A charge the gateway has accepted but not settled answers <c>PAYMENT_IN_PROGRESS</c> and holds the claim, so nobody charges meanwhile.</summary>
    [Fact]
    public async Task Pay_WhenTheChargeIsPending_AnswersInProgressAndHoldsTheClaim()
    {
        var gateway = BuildHost();
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Pending, null, null, null, null);
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-pending");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Contain(HostpanelErrorCodes.PAYMENT_IN_PROGRESS);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
        (await ClaimsOfAsync(gateway.Host, invoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>
    /// A method id that is not saved on the caller's account (another customer's, or invented) is refused with
    /// <c>PAYMENT_METHOD_NOT_OWNED</c> and the gateway is never charged.
    /// </summary>
    [Fact]
    public async Task Pay_WithAForeignOrUnknownMethodId_RefusesWithoutChargingAnything()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-foreign-method");

        var response = await PayAsync(owner, invoiceId, "pm_someone_elses");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>An account that has no customer at the module is refused with <c>NO_SAVED_PAYMENT_METHODS</c> and nothing is charged.</summary>
    [Fact]
    public async Task Pay_WithoutAGatewayCustomer_RefusesWithNoSavedPaymentMethods()
    {
        var gateway = BuildHost();
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, gateway.Host, "owner-no-customer");
        var admin = await BillingTestSeeds.AdminClientAsync(factory, gateway.Host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.NO_SAVED_PAYMENT_METHODS);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// A module that is not offered (not installed, or switched off) refuses with 409 <c>PAYMENT_METHOD_UNAVAILABLE</c>:
    /// the invoice is never marked paid by a payment path that cannot take money.
    /// </summary>
    [Fact]
    public async Task Pay_WhenTheModuleIsNotOffered_RefusesAndLeavesItUnpaid()
    {
        var gateway = BuildHost();
        gateway.Resolver.Offered = false;
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-unoffered");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>An already-paid invoice is refused with <c>INVOICE_NOT_PAYABLE</c> before the gateway is charged a second time.</summary>
    [Fact]
    public async Task Pay_AnAlreadyPaidInvoice_RefusesWithoutChargingAgain()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-paid-twice");
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>A body with no payment method id is refused by validation, not guessed at.</summary>
    [Fact]
    public async Task Pay_WithoutAMethodId_RefusesWithAValidationError()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-no-method-id");

        var response = await PayAsync(owner, invoiceId, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>A body with no gateway module is refused by validation.</summary>
    [Fact]
    public async Task Pay_WithoutAModule_RefusesWithAValidationError()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-no-module");

        var response = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid(), module: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// Paying a stranger's invoice is refused with <c>INVOICE_NOT_FOUND</c> before the gateway is reached, so the
    /// invoice stays unpaid.
    /// </summary>
    [Fact]
    public async Task Pay_AStrangersInvoice_Returns404WithInvoiceNotFoundAndChargesNothing()
    {
        var gateway = BuildHost();
        var (_, admin, invoiceId) = await SeedAsync(gateway, "owner-strangers-pay");
        var stranger = await BillingTestSeeds.RegisterStrangerAsync(factory, gateway.Host, "stranger-pay");

        var response = await PayAsync(stranger, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        gateway.Plugin.Charges.Should().BeEmpty();
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
    /// In the demo the same endpoint settles through the demo payment plugin behind the same capability: a method
    /// saved on the client's demo account is charged, and a method id of another customer is refused.
    /// </summary>
    [Fact]
    public async Task Pay_InTheDemo_SettlesThroughTheDemoPaymentPlugin()
    {
        var host = DemoTestHost.Enabled(factory);
        var (owner, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "owner-demo-pay");
        await SwitchStripeAsync(enabled: true);
        await BillingTestSeeds.LinkGatewayCustomerAsync(host, clientId, "stripe", _customerId);
        var state = host.Services.GetRequiredService<DemoStoredCredentialState>();
        var methodId = state.CompleteSession(state.OpenSession(_customerId))!.MethodRef;
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        try
        {
            var foreign = await PayAsync(owner, invoiceId, "demo-pm-someone-else", Guid.NewGuid(), "stripe");
            (await BillingTestSeeds.CodeOfAsync(foreign)).Should().Be(HostpanelErrorCodes.PAYMENT_METHOD_NOT_OWNED);
            (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));

            var response = await PayAsync(owner, invoiceId, methodId, Guid.NewGuid(), "stripe");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
            (await LedgerRowsAsync(host, invoiceId)).Should().Be(1);
        }
        finally
        {
            await SwitchStripeAsync(enabled: false);
        }
    }

    /// <summary>
    /// Switches the built-in card gateway on or off in the admin integrations, which its saved-method capability honours.
    /// Written through the ordinary host: the demo host refuses an administrator's writes, and the database is shared.
    /// </summary>
    /// <param name="enabled">Whether it should end up enabled.</param>
    /// <returns>A task that completes when the switch is saved.</returns>
    private async Task SwitchStripeAsync(bool enabled)
    {
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        var response = await admin.PutAsJsonAsync("/api/admin/integrations/stripe", new { isEnabled = enabled, config = new Dictionary<string, string>() });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Paying an invoice is not for anonymous callers.</summary>
    [Fact]
    public async Task PayRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/me/invoices/1/pay", new { gatewayModuleId = StoredCredentialHost.Module, paymentMethodId = SavedMethodId });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A body with no payment attempt is refused by validation before any charge.</summary>
    [Fact]
    public async Task Pay_WithoutAnAttemptId_RefusesWithAValidationError()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-no-attempt-id");

        var response = await PayAsync(owner, invoiceId, SavedMethodId, null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// The same payment attempt sent twice is one charge at the gateway: both requests carry the same idempotency
    /// key, built from the invoice and the attempt, so the gateway takes the money once.
    /// </summary>
    [Fact]
    public async Task Pay_SameAttemptTwice_ChargesOnce()
    {
        var gateway = BuildHost();
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        var (owner, _, invoiceId) = await SeedAsync(gateway, "owner-idempotent");
        var attemptId = Guid.NewGuid();

        await PayAsync(owner, invoiceId, SavedMethodId, attemptId);
        await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        gateway.Plugin.Charges.Should().ContainSingle()
            .Which.Key.Should().Be($"invoice-{invoiceId}-attempt-{attemptId:N}");
    }

    /// <summary>
    /// After a decline the same card is charged again at once by a new pay action: its new attempt is a new
    /// idempotency key, so the gateway does not replay the decline, and the invoice is paid.
    /// </summary>
    [Fact]
    public async Task Pay_NewAttemptAfterADecline_ChargesAgain()
    {
        var gateway = BuildHost();
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-retry-after-decline");
        var declinedAttempt = Guid.NewGuid();

        var declined = await PayAsync(owner, invoiceId, SavedMethodId, declinedAttempt);
        gateway.Plugin.OnCharge = i => new StoredChargeResult(StoredChargeOutcome.Succeeded, $"txn-retry-{Guid.NewGuid():N}", i.AmountMinor, i.Currency, null);
        var replayed = await PayAsync(owner, invoiceId, SavedMethodId, declinedAttempt);
        var retried = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid());

        declined.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        replayed.StatusCode.Should().Be(HttpStatusCode.BadGateway, "the gateway replays the decline for the same attempt");
        retried.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.Charges.Should().HaveCount(2);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(1);
    }

    /// <summary>
    /// A double click sends the same pay action twice at once. The invoice's payment claim lets one request reach
    /// the gateway; the other is told <c>PAYMENT_IN_PROGRESS</c> (or, if the first already finished, finds the settled
    /// charge) instead of charging too, so the invoice is paid once with one ledger row and the gateway is charged
    /// once. Sending the same action again once it finished answers success.
    /// </summary>
    [Fact]
    public async Task Pay_DoubleClick_RecordsOnePayment()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-double-click");
        var attemptId = Guid.NewGuid();

        var responses = await Task.WhenAll(
            Task.Run(() => PayAsync(owner, invoiceId, SavedMethodId, attemptId)),
            Task.Run(() => PayAsync(owner, invoiceId, SavedMethodId, attemptId)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent || r.StatusCode == HttpStatusCode.Conflict);
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.NoContent);
        foreach (var refused in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            (await BillingTestSeeds.CodeOfAsync(refused)).Should().Contain(HostpanelErrorCodes.PAYMENT_IN_PROGRESS);
        }

        (await PayAsync(owner, invoiceId, SavedMethodId, attemptId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(1);
        gateway.Plugin.Charges.Should().ContainSingle();
    }

    /// <summary>
    /// A retry of the attempt that already paid the invoice (its answer was lost) answers success: the gateway's
    /// charge is found by the attempt's key without charging again, and the settlement replays. The invoice keeps
    /// one ledger row.
    /// </summary>
    [Fact]
    public async Task Pay_SameAttemptAfterItSettled_AnswersSuccessWithoutChargingAgain()
    {
        var gateway = BuildHost();
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-retry-after-success");
        var attemptId = Guid.NewGuid();

        var first = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);
        var retried = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        retried.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.Charges.Should().ContainSingle();
        gateway.Plugin.ChargeLookups.Should().Be(1);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(1);
    }

    /// <summary>
    /// A new attempt on an invoice another attempt already paid finds no charge of its own at the gateway and is
    /// refused with <c>INVOICE_NOT_PAYABLE</c>, without charging anything.
    /// </summary>
    [Fact]
    public async Task Pay_NewAttemptAfterAnotherSettledIt_RefusesWithoutCharging()
    {
        var gateway = BuildHost();
        var (owner, _, invoiceId) = await SeedAsync(gateway, "owner-new-attempt-after-success");

        var first = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid());
        var second = await PayAsync(owner, invoiceId, SavedMethodId, Guid.NewGuid());

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BillingTestSeeds.CodeOfAsync(second)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        gateway.Plugin.Charges.Should().ContainSingle();
        gateway.Plugin.ChargeLookups.Should().Be(1);
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(1);
    }

    /// <summary>A gateway that cannot list the account's methods answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>, never a 500, and charges nothing.</summary>
    [Fact]
    public async Task Pay_WhenTheGatewayCannotListMethods_AnswersServiceUnavailableWithoutCharging()
    {
        var gateway = BuildHost();
        gateway.Plugin.MethodCallsThrow = true;
        var (owner, admin, invoiceId) = await SeedAsync(gateway, "owner-list-unavailable");

        var response = await PayAsync(owner, invoiceId, SavedMethodId);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        response.Headers.RetryAfter.Should().NotBeNull();
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>
    /// A retry of a settled attempt whose charge lookup the gateway cannot answer is told the gateway is unavailable
    /// rather than that nothing was taken, and charges nothing again.
    /// </summary>
    [Fact]
    public async Task Pay_SameAttemptAfterItSettled_WhenTheLookupFails_AnswersServiceUnavailable()
    {
        var gateway = BuildHost();
        var (owner, _, invoiceId) = await SeedAsync(gateway, "owner-lookup-fails");
        var attemptId = Guid.NewGuid();

        var first = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);
        gateway.Plugin.LookupThrows = true;
        var retried = await PayAsync(owner, invoiceId, SavedMethodId, attemptId);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        retried.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await BillingTestSeeds.CodeOfAsync(retried)).Should().Be(HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await LedgerRowsAsync(gateway.Host, invoiceId)).Should().Be(1);
    }
}
