using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.ChargeRenewalInvoice;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ChargeRenewalInvoiceCommand"/> against Postgres with the card gateway replaced by
/// <see cref="FakeCardPaymentGateway"/>: a renewal invoice is charged to the client's default card only when
/// the client agreed to that card being charged, a card that needs authentication or declines leaves the invoice
/// unpaid and tells the client once, and the same invoice is never charged twice, whether the job runs again,
/// the client pays it by hand at the same moment, or an earlier attempt died after the money was taken.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ChargeRenewalInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The saved card every seeded client owns in these tests.</summary>
    private const string CardId = "pm_auto_1";

    /// <summary>Builds a configured fake gateway listing <see cref="CardId"/> as the default card.</summary>
    /// <returns>The fake gateway.</returns>
    private static FakeCardPaymentGateway ConfiguredGateway() => new()
    {
        IsConfigured = true,
        SavedPaymentMethods = [new CardPaymentMethodDto(CardId, SavedPaymentMethodType.Card, "visa", "4242", 12, 2035, true)],
    };

    /// <summary>A consenting client's renewal invoice is charged to the default card and settled.</summary>
    [Fact]
    public async Task HandleAsync_AgreedCard_ChargesAndSettlesAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.ChargesByKey.Should().ContainSingle();
        gateway.LastChargeInstruction!.PaymentMethodId.Should().Be(CardId);
        (await StatusOfAsync(host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
        (await ClaimsOfAsync(host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().Be(InvoicePaymentClaimOutcome.Settled);
    }

    /// <summary>The job runs again after the invoice was charged: nothing is charged a second time.</summary>
    [Fact]
    public async Task HandleAsync_RunTwice_ChargesOnceAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);

        await InvokeAsync(host, seeded.InvoiceId);
        var second = await InvokeAsync(host, seeded.InvoiceId);

        second.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.ChargesByKey.Should().ContainSingle();
    }

    /// <summary>A card saved without the client's agreement (by staff, in the gateway's dashboard, before this feature) is never charged.</summary>
    [Fact]
    public async Task HandleAsync_NoAgreement_SkipsWithoutChargingAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: false);

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.LastChargeInstruction.Should().BeNull();
        (await StatusOfAsync(host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>An agreement revoked by the client stops the charge.</summary>
    [Fact]
    public async Task HandleAsync_AgreementRevoked_SkipsWithoutChargingAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);
        await MutateAsync(host, async context =>
        {
            var consent = await context.CardChargeConsents.SingleAsync(c => c.ClientId == seeded.ClientId);
            consent.Revoke("test", DateTimeOffset.UtcNow);
        });

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.LastChargeInstruction.Should().BeNull();
    }

    /// <summary>A client with card processing switched off is never charged.</summary>
    [Fact]
    public async Task HandleAsync_CardProcessingDisabled_SkipsWithoutChargingAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);
        await MutateAsync(host, async context =>
        {
            var client = await context.Clients.SingleAsync(c => c.Id == seeded.ClientId);
            client.UpdateSettings(true, false, false, disableCcProcessing: true, false, false, false);
        });

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
        gateway.LastChargeInstruction.Should().BeNull();
    }

    /// <summary>A deployment without card gateway keys charges nothing.</summary>
    [Fact]
    public async Task HandleAsync_GatewayNotConfigured_SkipsAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);
        gateway.IsConfigured = false;

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Skipped);
    }

    /// <summary>
    /// The card needs authentication (3-D Secure): the invoice stays unpaid, the client gets one e-mail with the
    /// pay link, and the next run neither charges nor writes again.
    /// </summary>
    [Fact]
    public async Task HandleAsync_AuthenticationRequired_LeavesUnpaidAndMailsOnceAsync()
    {
        var gateway = ConfiguredGateway();
        gateway.Charge = new CardChargeResult(false, null, CardGatewayFailure.AuthenticationRequired);
        var mail = new FakeEmailSender();
        var host = BuildHost(gateway, mail);
        var seeded = await SeedAsync(host, agreed: true);

        var first = await InvokeAsync(host, seeded.InvoiceId);
        var second = await InvokeAsync(host, seeded.InvoiceId);

        first.Value.Should().Be(RenewalChargeOutcome.NeedsAuthentication);
        second.Value.Should().Be(RenewalChargeOutcome.Skipped);
        (await StatusOfAsync(host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
        gateway.ChargesByKey.Should().ContainSingle();
        var message = MailAfter(mail, seeded).Should().ContainSingle().Subject;
        message.To.Should().Be(seeded.Email);
        message.Body.Should().Contain($"/client/invoices/{seeded.InvoiceId}/pay");
    }

    /// <summary>A declined card leaves the invoice unpaid, mails the client once with a different text, and is not retried.</summary>
    [Fact]
    public async Task HandleAsync_Declined_LeavesUnpaidAndMailsOnceAsync()
    {
        var gateway = ConfiguredGateway();
        gateway.Charge = new CardChargeResult(false, null, CardGatewayFailure.Refused);
        var mail = new FakeEmailSender();
        var host = BuildHost(gateway, mail);
        var seeded = await SeedAsync(host, agreed: true);

        var first = await InvokeAsync(host, seeded.InvoiceId);
        await InvokeAsync(host, seeded.InvoiceId);

        first.Value.Should().Be(RenewalChargeOutcome.Declined);
        (await StatusOfAsync(host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Unpaid);
        gateway.ChargesByKey.Should().ContainSingle();
        MailAfter(mail, seeded).Should().ContainSingle();
    }

    /// <summary>The two kinds of failure are told apart: the authentication mail and the decline mail are different texts.</summary>
    [Fact]
    public async Task HandleAsync_AuthenticationAndDecline_SendDifferentTextsAsync()
    {
        var authMail = new FakeEmailSender();
        var auth = ConfiguredGateway();
        auth.Charge = new CardChargeResult(false, null, CardGatewayFailure.AuthenticationRequired);
        var authHost = BuildHost(auth, authMail);
        var authSeeded = await SeedAsync(authHost, agreed: true);
        await InvokeAsync(authHost, authSeeded.InvoiceId);

        var declineMail = new FakeEmailSender();
        var decline = ConfiguredGateway();
        decline.Charge = new CardChargeResult(false, null, CardGatewayFailure.Refused);
        var declineHost = BuildHost(decline, declineMail);
        var declineSeeded = await SeedAsync(declineHost, agreed: true);
        await InvokeAsync(declineHost, declineSeeded.InvoiceId);

        MailAfter(authMail, authSeeded).Single().Subject.Should().NotBe(MailAfter(declineMail, declineSeeded).Single().Subject);
    }

    /// <summary>A gateway that cannot be reached leaves the claim held: nothing is known, nothing is mailed.</summary>
    [Fact]
    public async Task HandleAsync_GatewayUnavailable_HoldsTheClaimAndMailsNothingAsync()
    {
        var gateway = ConfiguredGateway();
        gateway.Charge = new CardChargeResult(false, null, CardGatewayFailure.Unavailable);
        var mail = new FakeEmailSender();
        var host = BuildHost(gateway, mail);
        var seeded = await SeedAsync(host, agreed: true);

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Unavailable);
        MailAfter(mail, seeded).Should().BeEmpty();
        (await ClaimsOfAsync(host, seeded.InvoiceId)).Should().ContainSingle().Which.Outcome.Should().BeNull();
    }

    /// <summary>
    /// The client pays the invoice by hand at the very moment the job charges it: exactly one charge reaches the
    /// gateway and the invoice is paid once.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ManualPaymentAtTheSameTime_ChargesOnceAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);
        var caller = await SignInAsync(host, seeded.Email);

        var automatic = Task.Run(() => InvokeAsync(host, seeded.InvoiceId));
        var manual = Task.Run(() => caller.PostAsJsonAsync($"/api/me/invoices/{seeded.InvoiceId}/pay", new { paymentMethodId = CardId, attemptId = Guid.NewGuid() }));
        await Task.WhenAll(automatic, manual);

        gateway.ChargesByKey.Should().ContainSingle();
        (await StatusOfAsync(host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
        (await manual).StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.Conflict);
    }

    /// <summary>
    /// An earlier attempt took the money and died before settling the invoice: the next run finds the charge
    /// by its key, settles the invoice and does not charge again.
    /// </summary>
    [Fact]
    public async Task HandleAsync_StaleClaimWhoseChargeWentThrough_SettlesWithoutChargingAgainAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);
        var staleKey = InvoicePaymentClaim.AutomaticKey(seeded.InvoiceId, Guid.NewGuid());
        var staleCharge = new CardChargeInstruction("cus", CardId, 10m, "USD", seeded.InvoiceId, staleKey);
        gateway.ChargesByKey[staleKey] = new CardChargeResult(true, $"txn-stale-{Guid.NewGuid():N}");
        gateway.ChargeInstructionsByKey[staleKey] = staleCharge;
        await MutateAsync(host, context =>
        {
            context.InvoicePaymentClaims.Add(InvoicePaymentClaim.Start(
                seeded.InvoiceId, InvoicePaymentClaimKind.Automatic, staleKey, DateTimeOffset.UtcNow.AddHours(-2), TimeSpan.FromMinutes(15)));
            return Task.CompletedTask;
        });

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.ChargesByKey.Should().ContainSingle();
        (await StatusOfAsync(host, seeded.InvoiceId)).Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>An earlier attempt that took nothing and died is expired and a new attempt charges.</summary>
    [Fact]
    public async Task HandleAsync_StaleClaimWithoutACharge_ExpiresItAndChargesAsync()
    {
        var gateway = ConfiguredGateway();
        var host = BuildHost(gateway, new FakeEmailSender());
        var seeded = await SeedAsync(host, agreed: true);
        await MutateAsync(host, context =>
        {
            context.InvoicePaymentClaims.Add(InvoicePaymentClaim.Start(
                seeded.InvoiceId, InvoicePaymentClaimKind.Automatic, InvoicePaymentClaim.AutomaticKey(seeded.InvoiceId, Guid.NewGuid()),
                DateTimeOffset.UtcNow.AddHours(-2), TimeSpan.FromMinutes(15)));
            return Task.CompletedTask;
        });

        var result = await InvokeAsync(host, seeded.InvoiceId);

        result.Value.Should().Be(RenewalChargeOutcome.Charged);
        gateway.ChargesByKey.Should().ContainSingle();
        (await ClaimsOfAsync(host, seeded.InvoiceId)).Select(c => c.Outcome).Should()
            .BeEquivalentTo([InvoicePaymentClaimOutcome.Expired, InvoicePaymentClaimOutcome.Settled]);
    }

    /// <summary>An invoice that does not exist is <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task HandleAsync_UnknownInvoice_RefusesNotFoundAsync()
    {
        var host = BuildHost(ConfiguredGateway(), new FakeEmailSender());

        var result = await InvokeAsync(host, int.MaxValue);

        result.ErrorCode.Should().Be(Application.Common.Errors.HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Builds a host with the gateway and the mail relay replaced and domain events suppressed.</summary>
    /// <param name="gateway">The fake card gateway.</param>
    /// <param name="mail">The fake mail relay.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway gateway, FakeEmailSender mail) =>
        EventHandlerTestHost.WithFakeRelay(factory, mail).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(gateway)));

    /// <summary>Seeds a signed-up client with a customer on file and an unpaid renewal invoice.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="agreed">Whether the client agreed to automatic charges for the default card.</param>
    /// <returns>The client, their e-mail and the invoice.</returns>
    private async Task<SeededRenewalInvoice> SeedAsync(WebApplicationFactory<Program> host, bool agreed)
    {
        var signedIn = await BillingTestSeeds.RegisterClientAsync(factory, host, "auto");
        var clientId = signedIn.ClientId;
        await BillingTestSeeds.StampCardCustomerAsync(host, clientId, $"cus_{Guid.NewGuid():N}"[..24]);

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
            invoice.AddServiceRenewal("Starter renewal", 10m, service.Id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMonths(1));
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;

            if (agreed)
            {
                var consent = CardChargeConsent.Request(clientId, $"seti_{Guid.NewGuid():N}", CardConsentSource.AddCard, null, null, DateTimeOffset.UtcNow);
                consent.Activate(CardId, DateTimeOffset.UtcNow);
                context.CardChargeConsents.Add(consent);
                await context.SaveChangesAsync(CancellationToken.None);
            }
        });

        return new SeededRenewalInvoice(clientId, signedIn.Email, invoiceId, MailBefore: host.Services.GetRequiredService<IEmailSender>() is FakeEmailSender relay ? relay.Sent.Count : 0);
    }

    /// <summary>The mails sent after seeding was done, which are the ones the charge sent.</summary>
    /// <param name="mail">The fake mail relay.</param>
    /// <param name="seeded">The seeded invoice.</param>
    /// <returns>The mails the charge sent.</returns>
    private static IReadOnlyList<Innovayse.Hostpanel.Domain.Notifications.EmailMessage> MailAfter(FakeEmailSender mail, SeededRenewalInvoice seeded) =>
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

    /// <summary>A seeded client, their e-mail and an unpaid renewal invoice.</summary>
    /// <param name="ClientId">The client.</param>
    /// <param name="Email">The client's e-mail.</param>
    /// <param name="InvoiceId">The invoice.</param>
    /// <param name="MailBefore">How many mails the relay held once seeding was done (sign-up sends its own).</param>
    private sealed record SeededRenewalInvoice(int ClientId, string Email, int InvoiceId, int MailBefore);
}
