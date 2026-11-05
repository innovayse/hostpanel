using Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;
using Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// The mail a stored method charge sends the client through <see cref="ApplyStoredChargeOutcomeCommand"/>, against
/// Postgres and the real bus with the mail relay replaced by <see cref="FakeEmailSender"/>: a decline mails the way to
/// pay another way, a charge that needs the payer mails the confirmation link, and a success or a pending charge
/// mails nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApplyStoredChargeOutcomeMailTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>ISO 4217 numeric code of USD, the currency every invoice here is issued in.</summary>
    private const string Usd = "840";

    /// <summary>The amount of the seeded invoices in minor units.</summary>
    private const long AmountMinor = 1005;

    /// <summary>The address the payer is sent to for the bank's confirmation.</summary>
    private const string ActionAddress = "https://bank.example/3ds";

    /// <summary>Opens an unresolved claim on a fresh invoice for a consenting client who can be mailed.</summary>
    /// <param name="scenario">The scenario to seed.</param>
    /// <returns>The client's address, the invoice and the open claim.</returns>
    private static async Task<(string Email, int InvoiceId, InvoicePaymentClaim Claim)> OpenClaimAsync(StoredCredentialScenario scenario)
    {
        var (clientId, email) = await scenario.SeedMailableClientAsync();
        await scenario.SeedConsentAsync(clientId);

        // An amount ending in 05 takes the money and then times out, so the claim stays open for the outcome to end.
        var invoiceId = await scenario.SeedInvoiceAsync(clientId, 10.05m);
        await scenario.InvokeAsync<Result<InvoiceChargeDto>>(new ChargeInvoiceStoredMethodCommand(invoiceId));
        var claim = (await scenario.ClaimsOfAsync(invoiceId)).Single();
        claim.Status.Should().Be(InvoicePaymentClaimStatus.Open);
        return (email, invoiceId, claim);
    }

    /// <summary>
    /// The mails to the client that come from the charge outcome. The welcome mail and the invoice-created mail go to the same
    /// address, so the outcome mails are told apart by what only they carry: the invoice page link or the confirmation address.
    /// </summary>
    /// <param name="emailSender">The fake relay that recorded every mail.</param>
    /// <param name="email">The client's address.</param>
    /// <param name="invoiceId">The invoice the charge belongs to.</param>
    /// <returns>The outcome mails sent to the client.</returns>
    private static IReadOnlyList<EmailMessage> OutcomeMails(FakeEmailSender emailSender, string email, int invoiceId)
    {
        return emailSender.Sent
            .Where(m => m.To == email && (m.Body.Contains($"/client/invoices/{invoiceId}") || m.Body.Contains(ActionAddress)))
            .ToList();
    }

    /// <summary>A decline mails the client once, from the declined template, with the invoice link to pay another way.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_Declined_MailsTheClientTheInvoiceLink()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, invoiceId, claim) = await OpenClaimAsync(scenario);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Declined, null, null, null, null));

        var message = OutcomeMails(emailSender, email, invoiceId).Should().ContainSingle().Subject;
        message.Subject.Should().Contain($"#{invoiceId}");
        message.Body.Should().Contain("10.05").And.Contain("USD").And.Contain($"/client/invoices/{invoiceId}");
        message.Body.Should().NotContain(ActionAddress);
    }

    /// <summary>A charge that needs the payer mails the client once, from the action template, with the confirmation link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_ActionRequired_MailsTheClientTheActionLink()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, invoiceId, claim) = await OpenClaimAsync(scenario);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.ActionRequired, null, null, null, ActionAddress));

        var message = OutcomeMails(emailSender, email, invoiceId).Should().ContainSingle().Subject;
        message.Subject.Should().Contain($"#{invoiceId}");
        message.Body.Should().Contain("10.05").And.Contain("USD").And.Contain(ActionAddress);
    }

    /// <summary>When the plugin gave no address, the confirmation mail falls back to the invoice page.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_ActionRequiredWithoutAddress_LinksTheInvoice()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, invoiceId, claim) = await OpenClaimAsync(scenario);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.ActionRequired, null, null, null, null));

        emailSender.Sent.Should().ContainSingle(m => m.To == email && m.Body.Contains($"/client/invoices/{invoiceId}"));
    }

    /// <summary>The same outcome applied again ends nothing new, so the client is mailed once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_DeclinedTwice_MailsOnce()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, invoiceId, claim) = await OpenClaimAsync(scenario);
        var command = new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Declined, null, null, null, null);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(command);
        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(command);

        OutcomeMails(emailSender, email, invoiceId).Should().ContainSingle();
    }

    /// <summary>A reported action required repeated for the same claim mails the link once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_ActionRequiredTwice_MailsOnce()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, _, claim) = await OpenClaimAsync(scenario);
        var command = new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.ActionRequired, null, null, null, ActionAddress);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(command);
        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(command);

        emailSender.Sent.Should().ContainSingle(m => m.To == email && m.Body.Contains(ActionAddress));
    }

    /// <summary>A charge that went through sends neither of the two mails.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_Success_MailsNeitherTemplate()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, invoiceId, claim) = await OpenClaimAsync(scenario);

        var result = await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Succeeded, "txn-mail-ok", AmountMinor, Usd, null));

        result.Value!.Status.Should().Be(InvoicePaymentClaimStatus.Settled);
        OutcomeMails(emailSender, email, invoiceId).Should().BeEmpty();
    }

    /// <summary>A charge the gateway has accepted but not settled mails nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_Pending_MailsNothing()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (email, invoiceId, claim) = await OpenClaimAsync(scenario);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Pending, null, null, null, null));

        OutcomeMails(emailSender, email, invoiceId).Should().BeEmpty();
    }

    /// <summary>The declined and action templates exist in English, Russian and Armenian once a mail needed them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Apply_Declined_SeedsBothTemplatesInEveryLanguage()
    {
        var emailSender = new FakeEmailSender();
        using var scenario = new StoredCredentialScenario(factory, emailSender);
        var (_, _, claim) = await OpenClaimAsync(scenario);

        await scenario.InvokeAndSettleAsync<Result<InvoiceChargeDto>>(
            new ApplyStoredChargeOutcomeCommand(claim.Id, StoredChargeOutcome.Declined, null, null, null, null));

        using var scope = scenario.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        foreach (var slug in new[] { CardChargeTemplatesSeeder.DeclinedSlug, CardChargeTemplatesSeeder.ActionRequiredSlug })
        {
            var languages = await context.EmailTemplates.AsNoTracking()
                .Where(t => t.Slug == slug)
                .Select(t => t.Language)
                .ToListAsync();
            languages.Should().BeEquivalentTo(["en", "ru", "hy"]);
        }
    }
}
