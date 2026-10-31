using Innovayse.Hostpanel.Application.Billing.Commands.ReleaseDomainRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ReleaseDomainRenewalInvoicesCommand"/> through the real <see cref="IMessageBus"/>: the
/// command a client renew order dispatches so the domain's open auto-renewal invoices stop billing it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReleaseDomainRenewalInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An invoice billing only this domain is cancelled and its credit goes back to the balance, in one save.</summary>
    [Fact]
    public async Task HandleAsync_OnlyThisDomain_CancelsAndReturnsCreditAsync()
    {
        var seeded = await SeedClientWithDomainsAsync();
        var invoiceId = await SeedInvoiceAsync(seeded.ClientId, i => AddRenewal(i, seeded.DomainId), clientCredit: 5m);

        var result = await ReleaseAsync(seeded.DomainId);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Cancelled);
        reloaded.Credit.Should().Be(0m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, seeded.ClientId)).Should().Be(5m);
    }

    /// <summary>An Unpaid invoice billing another domain too keeps the other line; the excess credit goes back.</summary>
    [Fact]
    public async Task HandleAsync_MixedUnpaid_RemovesOnlyThisDomainsLineAsync()
    {
        var seeded = await SeedClientWithDomainsAsync();

        // 12 + 12 billed, 20 of credit applied: after this domain's 12 goes, 8 of credit is excess.
        var invoiceId = await SeedInvoiceAsync(
            seeded.ClientId,
            i =>
            {
                AddRenewal(i, seeded.DomainId);
                AddRenewal(i, seeded.OtherDomainId);
            },
            clientCredit: 20m);

        var result = await ReleaseAsync(seeded.DomainId);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Unpaid);
        reloaded.Items.Should().ContainSingle().Which.RegisteredDomainId.Should().Be(seeded.OtherDomainId);
        reloaded.Credit.Should().Be(12m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, seeded.ClientId)).Should().Be(8m);
    }

    /// <summary>A PaymentPending invoice refuses the release with <c>RENEWAL_INVOICE_OPEN</c> and is left as it was.</summary>
    [Fact]
    public async Task HandleAsync_PaymentPending_RefusesAndChangesNothingAsync()
    {
        var seeded = await SeedClientWithDomainsAsync();
        var invoiceId = await SeedInvoiceAsync(seeded.ClientId, i => AddRenewal(i, seeded.DomainId));
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

            // No domain method reaches PaymentPending today, so the status is written directly.
            await context.Invoices.Where(i => i.Id == invoiceId)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, InvoiceStatus.PaymentPending), CancellationToken.None);
        }

        var result = await ReleaseAsync(seeded.DomainId);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.PaymentPending);
        reloaded.Items.Should().ContainSingle();
    }

    /// <summary>
    /// An invoice that already took a payment refuses the release with <c>RENEWAL_INVOICE_OPEN</c>: cancelling
    /// it would strand the money and dropping the line could leave the payment above the total.
    /// </summary>
    [Fact]
    public async Task HandleAsync_PartPaid_RefusesAndChangesNothingAsync()
    {
        var seeded = await SeedClientWithDomainsAsync();
        var invoiceId = await SeedInvoiceAsync(seeded.ClientId, i =>
        {
            AddRenewal(i, seeded.DomainId);
            AddRenewal(i, seeded.OtherDomainId);
            i.AddPayment(DateTimeOffset.UtcNow, PaymentMethodNames.Manual, "part", 20m, 0m, null, TransactionSource.Manual);
        });

        var result = await ReleaseAsync(seeded.DomainId);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Unpaid);
        reloaded.Items.Should().HaveCount(2);
        reloaded.Total.Should().Be(24m);
    }

    /// <summary>A domain on no open invoice is released without any change.</summary>
    [Fact]
    public async Task HandleAsync_NoOpenInvoice_SucceedsAsync()
    {
        var seeded = await SeedClientWithDomainsAsync();

        var result = await ReleaseAsync(seeded.DomainId);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>Seeds a client with two domains.</summary>
    /// <returns>The client, the domain to release and another domain of the same client.</returns>
    private async Task<SeededDomains> SeedClientWithDomainsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: true, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        var other = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: true, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.AddRange(domain, other);
        await context.SaveChangesAsync(CancellationToken.None);
        return new SeededDomains(clientId, domain.Id, other.Id);
    }

    /// <summary>Seeds an Unpaid invoice for the client, shaped by <paramref name="build"/>.</summary>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="build">Adds lines to the new invoice.</param>
    /// <param name="clientCredit">Credit moved from the client's balance onto the invoice; 0 for none.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedInvoiceAsync(int clientId, Action<Invoice> build, decimal clientCredit = 0m)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(10), "USD", DateTimeOffset.UtcNow);
        build(invoice);
        if (clientCredit > 0)
        {
            var client = await context.Clients.FirstAsync(c => c.Id == clientId, CancellationToken.None);
            client.AddCredit(clientCredit);
            client.DeductCredit(clientCredit);
            invoice.ApplyCredit(clientCredit);
        }

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Adds a 12.00 renewal line for <paramref name="domainId"/> starting in ten days.</summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="domainId">The domain.</param>
    private static void AddRenewal(Invoice invoice, int domainId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(10);
        invoice.AddDomainRenewal("Domain renewal", 12m, domainId, start, start.AddYears(1));
    }

    /// <summary>Dispatches the command from a fresh scope.</summary>
    /// <param name="domainId">The domain to release.</param>
    /// <returns>The command's result.</returns>
    private async Task<Result> ReleaseAsync(int domainId)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(new ReleaseDomainRenewalInvoicesCommand(domainId));
    }

    /// <summary>Reloads an invoice and its lines, untracked, from a fresh scope.</summary>
    /// <param name="id">The invoice id.</param>
    /// <returns>The invoice as stored.</returns>
    private async Task<Invoice> LoadInvoiceAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Items).FirstAsync(i => i.Id == id, CancellationToken.None);
    }

    /// <summary>A seeded client and two of its domains.</summary>
    /// <param name="ClientId">The client.</param>
    /// <param name="DomainId">The domain the tests release.</param>
    /// <param name="OtherDomainId">Another domain of the same client.</param>
    private sealed record SeededDomains(int ClientId, int DomainId, int OtherDomainId);
}
