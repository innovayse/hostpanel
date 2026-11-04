using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.ChargeRenewalInvoice;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ChargeRenewalInvoiceCommand"/> against Postgres with the saved-method capability replaced by
/// <see cref="FakeStoredCredentialPlugin"/>: a renewal invoice is charged to the client's default card only when the
/// client agreed to that card being charged, a card that needs authentication or declines leaves the invoice unpaid
/// and tells the client once, and the same invoice is never charged twice, whether the job runs again, the client
/// pays it by hand at the same moment, or an earlier attempt died after the money was taken. A claim is ended only
/// when the money is accounted for.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChargeRenewalInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved card every seeded client owns in these tests.</summary>
    private const string CardId = "pm_auto_1";

    /// <summary>A consenting client's renewal invoice is charged to the default card and settled.</summary>
    [Fact]
    public async Task HandleAsync_AgreedCard_ChargesAndSettlesAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Charged);
        var charge = gateway.Plugin.Charges.Should().ContainSingle().Subject;
        charge.Value.Instruction.MethodRef.Should().Be(CardId);
        charge.Value.Instruction.CustomerRef.Should().Be(seeded.CustomerRef);
        charge.Key.Should().StartWith($"invoice-{seeded.InvoiceId}-auto-");
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
        var claim = (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Subject;
        claim.Outcome.Should().Be(InvoicePaymentClaimOutcome.Settled);
        claim.Kind.Should().Be(InvoicePaymentClaimKind.Automatic);
        claim.GatewayModuleId.Should().Be(StoredCredentialHost.Module);
    }

    /// <summary>The job runs again after the invoice was charged: nothing is charged a second time.</summary>
    [Fact]
    public async Task HandleAsync_RunTwice_ChargesOnceAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);

        await InvokeAsync(gateway.Host, seeded.InvoiceId);
        var second = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        second.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().ContainSingle();
    }

    /// <summary>A card saved without the client's agreement (by staff, in the gateway's dashboard) is never charged.</summary>
    [Fact]
    public async Task HandleAsync_NoAgreement_SkipsWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: false);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>An agreement withdrawn by the client stops the charge.</summary>
    [Fact]
    public async Task HandleAsync_AgreementRevoked_SkipsWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        await MutateAsync(gateway.Host, async context =>
        {
            var consent = await context.CardChargeConsents.SingleAsync(c => c.ClientId == seeded.ClientId);
            consent.Revoke(CardConsentRevocationReason.WithdrawnByClient, DateTimeOffset.UtcNow);
        });

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>A card that is not the account's default is not charged: the agreement is for the default card.</summary>
    [Fact]
    public async Task HandleAsync_AgreedCardIsNotTheDefault_SkipsWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        gateway.Plugin.Methods.Clear();
        gateway.Plugin.Methods.AddRange([StoredCredentialHost.Card("pm_other_default"), StoredCredentialHost.Card(CardId, isDefault: false)]);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>A client with card processing switched off is never charged.</summary>
    [Fact]
    public async Task HandleAsync_CardProcessingDisabled_SkipsWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        await ClientSettingsSeeds.SetAsync(factory, seeded.ClientId, disableCcProcessing: true);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>A suspended account is never charged.</summary>
    [Fact]
    public async Task HandleAsync_SuspendedClient_SkipsWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        await ClientStatusSeeds.SetAsync(factory, seeded.ClientId, ClientStatus.Suspended);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>A module that is no longer offered (plugin disabled) charges nothing.</summary>
    [Fact]
    public async Task HandleAsync_ModuleNotOffered_SkipsAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        gateway.Resolver.Offered = false;

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>An invoice that is not a renewal is never charged by this job.</summary>
    [Fact]
    public async Task HandleAsync_NonRenewalInvoice_SkipsAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true, renewal: false);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
    }

    /// <summary>A gateway that cannot list the account's cards charges nothing and says it is unavailable.</summary>
    [Fact]
    public async Task HandleAsync_CardListFails_IsUnavailableWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        gateway.Plugin.MethodCallsThrow = true;

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Unavailable);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().BeEmpty();
    }

    /// <summary>
    /// The card needs authentication (3-D Secure): the invoice stays unpaid, the claim is closed as failed, the client
    /// gets one e-mail with the pay link, and the next run neither charges nor writes again.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ActionRequired_LeavesUnpaidClosesTheClaimAndMailsOnceAsync()
    {
        var mail = new FakeEmailSender();
        var gateway = BuildHost(mail);
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.ActionRequired, null, null, null, "https://bank.example.test/3ds");
        var seeded = await SeedAsync(gateway, agreed: true);

        var first = await InvokeAsync(gateway.Host, seeded.InvoiceId);
        var second = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        first.Value.Should().Be(RenewalChargeOutcome.NeedsAuthentication);
        second.Value.Should().Be(RenewalChargeOutcome.Skipped);
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Failed);
        var message = MailAfter(mail, seeded).Should().ContainSingle().Subject;
        message.To.Should().Be(seeded.Email);
        message.Body.Should().Contain($"/client/invoices/{seeded.InvoiceId}/pay");
    }

    /// <summary>A declined card leaves the invoice unpaid, closes the claim as failed, mails the client once, and is not retried.</summary>
    [Fact]
    public async Task HandleAsync_Declined_LeavesUnpaidClosesTheClaimAndMailsOnceAsync()
    {
        var mail = new FakeEmailSender();
        var gateway = BuildHost(mail);
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        var seeded = await SeedAsync(gateway, agreed: true);

        var first = await InvokeAsync(gateway.Host, seeded.InvoiceId);
        var second = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        first.Value.Should().Be(RenewalChargeOutcome.Declined);
        second.Value.Should().Be(RenewalChargeOutcome.Skipped);
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Failed);
        MailAfter(mail, seeded).Should().ContainSingle();
    }

    /// <summary>The two kinds of failure are told apart: the authentication mail and the decline mail are different texts.</summary>
    [Fact]
    public async Task HandleAsync_AuthenticationAndDecline_SendDifferentTextsAsync()
    {
        var authMail = new FakeEmailSender();
        var auth = BuildHost(authMail);
        auth.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.ActionRequired, null, null, null, null);
        var authSeeded = await SeedAsync(auth, agreed: true);
        await InvokeAsync(auth.Host, authSeeded.InvoiceId);

        var declineMail = new FakeEmailSender();
        var decline = BuildHost(declineMail);
        decline.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Declined, null, null, null, null);
        var declineSeeded = await SeedAsync(decline, agreed: true);
        await InvokeAsync(decline.Host, declineSeeded.InvoiceId);

        MailAfter(authMail, authSeeded).Single().Subject.Should().NotBe(MailAfter(declineMail, declineSeeded).Single().Subject);
    }

    /// <summary>A charge the gateway has not settled keeps the claim held: the job answers busy, mails nothing and never charges a second time.</summary>
    [Fact]
    public async Task HandleAsync_Pending_HoldsTheClaimAndAnswersBusyAsync()
    {
        var mail = new FakeEmailSender();
        var gateway = BuildHost(mail);
        gateway.Plugin.OnCharge = _ => new StoredChargeResult(StoredChargeOutcome.Pending, null, null, null, null);
        var seeded = await SeedAsync(gateway, agreed: true);

        var first = await InvokeAsync(gateway.Host, seeded.InvoiceId);
        var second = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        first.Value.Should().Be(RenewalChargeOutcome.Busy);
        second.Value.Should().Be(RenewalChargeOutcome.Busy);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
        MailAfter(mail, seeded).Should().BeEmpty();
    }

    /// <summary>A gateway that cannot be reached leaves the claim held: nothing is known, nothing is mailed.</summary>
    [Fact]
    public async Task HandleAsync_GatewayUnavailable_HoldsTheClaimAndMailsNothingAsync()
    {
        var mail = new FakeEmailSender();
        var gateway = BuildHost(mail);
        gateway.Plugin.ThrowsBeforeCharging = true;
        var seeded = await SeedAsync(gateway, agreed: true);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Unavailable);
        MailAfter(mail, seeded).Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>
    /// The gateway takes the money and the answer is lost: the claim stays held and nothing is charged again. Once the
    /// claim is stale the next run finds the charge by its key and settles the invoice without a second gateway charge.
    /// </summary>
    [Fact]
    public async Task HandleAsync_AnswerLost_KeepsTheClaimThenSettlesFromTheLookupWithoutChargingAgainAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        gateway.Plugin.ThrowsAfterCharging = true;
        var seeded = await SeedAsync(gateway, agreed: true);

        var first = await InvokeAsync(gateway.Host, seeded.InvoiceId);
        var whileHeld = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        first.Value.Should().Be(RenewalChargeOutcome.Unavailable);
        whileHeld.Value.Should().Be(RenewalChargeOutcome.Busy);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();

        gateway.Plugin.ThrowsAfterCharging = false;
        await AgeClaimsAsync(gateway.Host, seeded.InvoiceId);
        var later = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        later.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Settled);
        (await LedgerRowsAsync(gateway.Host, seeded.InvoiceId)).Should().Be(1);
    }

    /// <summary>The client withdraws the agreement after the job took the claim: nothing is charged and the claim is ended.</summary>
    [Fact]
    public async Task HandleAsync_AgreementWithdrawnAfterTheClaimIsTaken_SkipsAndEndsTheClaimAsync()
    {
        var withdraw = new ClaimTakenInterceptor(db => db.Set<CardChargeConsent>()
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, CardConsentStatus.Revoked)));
        var gateway = BuildHost(new FakeEmailSender(), withdraw);
        var seeded = await SeedAsync(gateway, agreed: true);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        withdraw.Fired.Should().BeTrue();
        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Expired);
    }

    /// <summary>The invoice is paid another way after the job took the claim: nothing is charged and the claim is ended.</summary>
    [Fact]
    public async Task HandleAsync_InvoicePaidAfterTheClaimIsTaken_SkipsAndEndsTheClaimAsync()
    {
        var pay = new ClaimTakenInterceptor(db => db.Set<Invoice>()
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, InvoiceStatus.Paid)));
        var gateway = BuildHost(new FakeEmailSender(), pay);
        var seeded = await SeedAsync(gateway, agreed: true);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        pay.Fired.Should().BeTrue();
        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Expired);
    }

    /// <summary>
    /// The gateway reports a different amount than was asked for: nothing is recorded on that word, the job fails with
    /// the upstream error and the claim stays held, so nobody charges a second time.
    /// </summary>
    [Fact]
    public async Task HandleAsync_AmountMismatch_IsNotRecordedAndHoldsTheClaimAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        gateway.Plugin.OnCharge = i => new StoredChargeResult(StoredChargeOutcome.Succeeded, $"txn-{i.IdempotencyKey}", i.AmountMinor - 1, i.Currency, null);
        var seeded = await SeedAsync(gateway, agreed: true);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
        (await LedgerRowsAsync(gateway.Host, seeded.InvoiceId)).Should().Be(0);
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
        (await InvokeAsync(gateway.Host, seeded.InvoiceId)).Value.Should().Be(RenewalChargeOutcome.Busy);
    }

    /// <summary>
    /// The money was taken but settlement refuses it (the invoice was cancelled meanwhile) and the ledger shows it
    /// held for an operator, with no gateway to refund through: the claim stays held, never ended on money nobody has
    /// accounted for.
    /// </summary>
    [Fact]
    public async Task HandleAsync_SettlementRefusedAndMoneyHeldForTheOperator_KeepsTheClaimHeldAsync()
    {
        var gateway = BuildHost(new FakeEmailSender(), new FakeCardPaymentGateway { IsConfigured = false });
        var seeded = await SeedAsync(gateway, agreed: true);
        gateway.Plugin.BeforeCharge = _ => CancelAsync(gateway.Host, seeded.InvoiceId);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
        (await HandedBackRowsAsync(gateway.Host, gateway.Plugin.Charges.Single().Value.Result.TransactionId!))
            .Should().ContainSingle().Which.AmountOut.Should().Be(0m);
    }

    /// <summary>The money was taken but settlement refuses it and the card gateway refunded it: the money is accounted for, so the claim ends as failed.</summary>
    [Fact]
    public async Task HandleAsync_SettlementRefusedAndMoneyRefunded_EndsTheClaimFailedAsync()
    {
        var card = new FakeCardPaymentGateway { IsConfigured = true };
        var gateway = BuildHost(new FakeEmailSender(), card);
        var seeded = await SeedAsync(gateway, agreed: true);
        gateway.Plugin.BeforeCharge = _ => CancelAsync(gateway.Host, seeded.InvoiceId);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.IsFailure.Should().BeTrue();
        card.RefundInstructions.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Failed);
    }

    /// <summary>The money was taken but settlement refuses it and, the card gateway refusing the refund, it was credited to the balance: the claim ends as failed.</summary>
    [Fact]
    public async Task HandleAsync_SettlementRefusedAndMoneyCredited_EndsTheClaimFailedAsync()
    {
        var card = new FakeCardPaymentGateway { IsConfigured = true, RefusesRefunds = true };
        var gateway = BuildHost(new FakeEmailSender(), card);
        var seeded = await SeedAsync(gateway, agreed: true);
        gateway.Plugin.BeforeCharge = _ => CancelAsync(gateway.Host, seeded.InvoiceId);

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.IsFailure.Should().BeTrue();
        (await BillingTestSeeds.CreditBalanceOfAsync(gateway.Host, seeded.ClientId)).Should().Be(10m);
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Failed);
    }

    /// <summary>
    /// The client pays the invoice by hand while the job's charge is at the gateway: the manual payment is told
    /// <c>PAYMENT_IN_PROGRESS</c>, exactly one charge reaches the gateway and the invoice is paid once.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ManualPaymentWhileTheJobCharges_ChargesOnceAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        var caller = await SignInAsync(gateway.Host, seeded.Email);
        var atGateway = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.Plugin.BeforeCharge = async _ =>
        {
            atGateway.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };

        var automatic = Task.Run(() => InvokeAsync(gateway.Host, seeded.InvoiceId));
        await atGateway.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var manual = await PayByHandAsync(caller, seeded.InvoiceId);
        release.SetResult();
        var charged = await automatic;

        manual.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(manual)).Should().Contain(HostpanelErrorCodes.PAYMENT_IN_PROGRESS);
        charged.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
        (await LedgerRowsAsync(gateway.Host, seeded.InvoiceId)).Should().Be(1);
    }

    /// <summary>The job meets the client's manual payment already at the gateway: the job answers busy and charges nothing.</summary>
    [Fact]
    public async Task HandleAsync_ManualPaymentAlreadyCharging_AnswersBusyAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        var caller = await SignInAsync(gateway.Host, seeded.Email);
        var atGateway = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.Plugin.BeforeCharge = async _ =>
        {
            atGateway.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };

        var manual = Task.Run(() => PayByHandAsync(caller, seeded.InvoiceId));
        await atGateway.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var automatic = await InvokeAsync(gateway.Host, seeded.InvoiceId);
        release.SetResult();
        var paid = await manual;

        automatic.Value.Should().Be(RenewalChargeOutcome.Busy);
        paid.StatusCode.Should().Be(HttpStatusCode.NoContent);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await LedgerRowsAsync(gateway.Host, seeded.InvoiceId)).Should().Be(1);
    }

    /// <summary>
    /// An earlier attempt took the money and died before settling the invoice: the next run finds the charge by its
    /// key, settles the invoice and does not charge again.
    /// </summary>
    [Fact]
    public async Task HandleAsync_StaleClaimWhoseChargeWentThrough_SettlesWithoutChargingAgainAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        var staleKey = InvoicePaymentClaim.AutomaticKey(seeded.InvoiceId, Guid.NewGuid());
        RecordCharge(gateway, seeded, staleKey);
        await AddClaimAsync(gateway.Host, seeded.InvoiceId, staleKey, DateTimeOffset.UtcNow.AddHours(-2));

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await StatusOfAsync(gateway.Host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>An earlier attempt that took nothing and died is expired and a new attempt charges.</summary>
    [Fact]
    public async Task HandleAsync_StaleClaimWithoutACharge_ExpiresItAndChargesAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        await AddClaimAsync(
            gateway.Host, seeded.InvoiceId, InvoicePaymentClaim.AutomaticKey(seeded.InvoiceId, Guid.NewGuid()), DateTimeOffset.UtcNow.AddHours(-2));

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.Plugin.Charges.Should().ContainSingle();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Select(c => c.Outcome).Should()
            .BeEquivalentTo([InvoicePaymentClaimOutcome.Expired, InvoicePaymentClaimOutcome.Settled]);
    }

    /// <summary>A stale claim whose lookup the gateway cannot answer keeps the invoice held and charges nothing.</summary>
    [Fact]
    public async Task HandleAsync_StaleClaimWhenTheLookupFails_AnswersBusyWithoutChargingAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());
        var seeded = await SeedAsync(gateway, agreed: true);
        await AddClaimAsync(
            gateway.Host, seeded.InvoiceId, InvoicePaymentClaim.AutomaticKey(seeded.InvoiceId, Guid.NewGuid()), DateTimeOffset.UtcNow.AddHours(-2));
        gateway.Plugin.LookupThrows = true;

        var result = await InvokeAsync(gateway.Host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Busy);
        gateway.Plugin.Charges.Should().BeEmpty();
        (await ClaimsOfAsync(gateway.Host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>An invoice that does not exist is <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownInvoice_RefusesNotFoundAsync()
    {
        var gateway = BuildHost(new FakeEmailSender());

        var result = await InvokeAsync(gateway.Host, int.MaxValue);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Records a charge the gateway took under a key, as an earlier attempt that died after charging left it.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="seeded">The seeded invoice.</param>
    /// <param name="key">The attempt's idempotency key.</param>
    private static void RecordCharge(StoredCredentialHost gateway, SeededRenewalInvoice seeded, string key)
    {
        var instruction = new StoredChargeInstruction(seeded.CustomerRef, CardId, 1000, "USD", seeded.InvoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture), key, null);
        gateway.Plugin.Charges[key] = (instruction, new StoredChargeResult(StoredChargeOutcome.Succeeded, $"txn-stale-{Guid.NewGuid():N}", 1000, "USD", null));
    }

    /// <summary>Builds a host with the capability, the mail relay and optionally a card gateway replaced.</summary>
    /// <param name="mail">The fake mail relay.</param>
    /// <param name="interceptors">Interceptors every data context of the host is built with.</param>
    /// <returns>The host with its fake capability, listing <see cref="CardId"/> as the default card.</returns>
    private StoredCredentialHost BuildHost(FakeEmailSender mail, params IInterceptor[] interceptors) =>
        BuildHost(mail, null, interceptors);

    /// <summary>Builds a host with the capability, the mail relay and a fake card gateway replaced.</summary>
    /// <param name="mail">The fake mail relay.</param>
    /// <param name="card">The fake one-off card gateway settlement hands money back through, or null for the host's own.</param>
    /// <param name="interceptors">Interceptors every data context of the host is built with.</param>
    /// <returns>The host with its fake capability, listing <see cref="CardId"/> as the default card.</returns>
    private StoredCredentialHost BuildHost(FakeEmailSender mail, FakeCardPaymentGateway? card, params IInterceptor[] interceptors)
    {
        var gateway = StoredCredentialHost.Create(EventHandlerTestHost.WithFakeRelay(factory, mail), card, interceptors);
        gateway.Plugin.Methods.Add(StoredCredentialHost.Card(CardId));
        return gateway;
    }

    /// <summary>Seeds a signed-up client with a customer on file and an unpaid invoice.</summary>
    /// <param name="gateway">The fake capability host.</param>
    /// <param name="agreed">Whether the client agreed to automatic charges for the default card.</param>
    /// <param name="renewal">Whether the invoice is a service renewal.</param>
    /// <returns>The client, their e-mail, their gateway customer and the invoice.</returns>
    private async Task<SeededRenewalInvoice> SeedAsync(StoredCredentialHost gateway, bool agreed, bool renewal = true)
    {
        var host = gateway.Host;
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, host, "auto");
        var clientId = signedIn.ClientId;
        var customerRef = $"cus_{Guid.NewGuid():N}"[..24];
        await gateway.LinkAsync(clientId, customerRef);

        var invoiceId = 0;
        await MutateAsync(host, async context =>
        {
            var group = ProductGroup.Create($"auto-charge-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);
            var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 10m, 100m, DateTimeOffset.UtcNow);
            context.Products.Add(product);
            await context.SaveChangesAsync(CancellationToken.None);
            var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
            await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);

            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            if (renewal)
            {
                invoice.AddServiceRenewal("Starter renewal", 10m, service.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1));
            }
            else
            {
                invoice.AddItem("Setup fee", 10m, 1);
            }

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        });

        if (agreed)
        {
            await gateway.AddConsentAsync(clientId, CardId);
        }

        return new SeededRenewalInvoice(
            clientId, signedIn.Email, invoiceId, customerRef, host.Services.GetRequiredService<IEmailSender>() is FakeEmailSender relay ? relay.Sent.Count : 0);
    }

    /// <summary>The mails sent after seeding was done, which are the ones the charge sent.</summary>
    /// <param name="mail">The fake mail relay.</param>
    /// <param name="seeded">The seeded invoice.</param>
    /// <returns>The mails the charge sent.</returns>
    private static IReadOnlyList<Domain.Notifications.EmailMessage> MailAfter(FakeEmailSender mail, SeededRenewalInvoice seeded) =>
        [.. mail.Sent.Skip(seeded.MailBefore)];

    /// <summary>Signs a seeded client in again, for a call made as them.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="email">The client's e-mail.</param>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> host, string email)
    {
        var http = host.CreateClient();
        var token = await factory.GetClientTokenAsync(email, BillingTestSeeds.TestPassword);
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>Pays an invoice by hand with the default card.</summary>
    /// <param name="caller">The paying client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> PayByHandAsync(HttpClient caller, int invoiceId) =>
        caller.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/pay",
            new { gatewayModuleId = StoredCredentialHost.Module, paymentMethodId = CardId, attemptId = Guid.NewGuid() });

    /// <summary>Runs a change against the host's database and saves it.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="change">The change.</param>
    private static async Task MutateAsync(WebApplicationFactory<Program> host, Func<IHostpanelDbContext, Task> change)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await change(context);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Cancels an invoice straight in the database, as staff would between the job's read and its settlement.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>A task that completes when the invoice is cancelled.</returns>
    private static async Task CancelAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.Invoices.Where(i => i.Id == invoiceId).ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, InvoiceStatus.Cancelled));
    }

    /// <summary>Makes every open claim of an invoice stale, as if its holder died a long time ago.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>A task that completes when the claims are aged.</returns>
    private static async Task AgeClaimsAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await context.InvoicePaymentClaims
            .Where(c => c.InvoiceId == invoiceId && c.Outcome == null)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.ExpiresAt, DateTimeOffset.UtcNow.AddHours(-1)));
    }

    /// <summary>Adds an unfinished automatic claim that started long ago.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="key">The claim's gateway key.</param>
    /// <param name="startedAt">When the claim started; its lifetime has long passed.</param>
    private static Task AddClaimAsync(WebApplicationFactory<Program> host, int invoiceId, string key, DateTimeOffset startedAt) =>
        MutateAsync(host, context =>
        {
            context.InvoicePaymentClaims.Add(InvoicePaymentClaim.Create(
                invoiceId, StoredCredentialHost.Module, InvoicePaymentClaimKind.Automatic, key, startedAt, TimeSpan.FromMinutes(15)));
            return Task.CompletedTask;
        });

    /// <summary>Reads an invoice's status.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status.</returns>
    private static async Task<InvoiceStatus> StatusOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.Status).SingleAsync();
    }

    /// <summary>Counts the ledger rows written against an invoice.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The number of ledger rows.</returns>
    private static async Task<int> LedgerRowsAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().CountAsync(t => t.InvoiceId == invoiceId);
    }

    /// <summary>Reads the ledger rows that hand a payment back (no invoice) under a gateway reference.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="transactionId">The gateway reference.</param>
    /// <returns>The rows.</returns>
    private static async Task<List<Transaction>> HandedBackRowsAsync(WebApplicationFactory<Program> host, string transactionId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking().Where(t => t.TransactionId == transactionId && t.InvoiceId == null).ToListAsync();
    }

    /// <summary>Reads the payment claims of an invoice.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The claims, oldest first.</returns>
    private static async Task<List<InvoicePaymentClaim>> ClaimsOfAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.InvoicePaymentClaims.AsNoTracking().Where(c => c.InvoiceId == invoiceId).OrderBy(c => c.Id).ToListAsync();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="invoiceId">The invoice to charge.</param>
    /// <returns>The handler's result.</returns>
    private static async Task<Result<RenewalChargeOutcome>> InvokeAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<RenewalChargeOutcome>>(new ChargeRenewalInvoiceCommand(invoiceId));
    }

    /// <summary>A seeded client, their e-mail, their gateway customer and an unpaid invoice.</summary>
    /// <param name="ClientId">The client.</param>
    /// <param name="Email">The client's e-mail.</param>
    /// <param name="InvoiceId">The invoice.</param>
    /// <param name="CustomerRef">The client's account at the fake module.</param>
    /// <param name="MailBefore">How many mails the relay held once seeding was done (sign-up sends its own).</param>
    private sealed record SeededRenewalInvoice(int ClientId, string Email, int InvoiceId, string CustomerRef, int MailBefore);
}
