using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the client-facing <c>RenewMyDomain</c> use case:
/// <c>POST /api/me/domains/{id}/renew</c>, which places an order to renew the caller's own
/// domain rather than renewing it outright. The admin equivalent is <c>RenewDomain</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RenewMyDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A renewal order cannot be placed against a stranger's domain, refused before an order
    /// number is spent, by the same ownership check every other action here goes through.
    /// </summary>
    [Fact]
    public async Task RenewMyDomain_RefusesAStrangersDomainAsync()
    {
        var (_, _, domainId, stranger) =
            await DomainsTestSeeds.SeedOwnerWithDomainAsync(factory, "renew-my-domain");

        var response = await stranger.PostAsJsonAsync(
            $"/api/me/domains/{domainId}/renew", new { years = 1, paymentMethod = "bank-gateway" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    [Fact]
    public async Task RenewMyDomain_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/renew", new { years = 1, paymentMethod = "bank-gateway" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Arranges a domain the owner can place a renew order for: a second domain of the same owner,
    /// the bank-transfer method switched on, an active domain product, and a ".com" renew price.
    /// </summary>
    /// <param name="label">A short label folded into the owner's unique email.</param>
    /// <returns>The authenticated owner, its client id, the domain to renew and another of its domains.</returns>
    private async Task<RenewableDomainScenario> ArrangeAsync(string label)
    {
        var admin = await DomainsTestSeeds.AdminClientAsync(factory);
        var (owner, ownerClientId) = await DomainsTestSeeds.RegisterOwnerAsync(factory, label);
        var domainId = await DomainsTestSeeds.RegisterDomainAsync(admin, ownerClientId);
        var otherDomainId = await DomainsTestSeeds.RegisterDomainAsync(admin, ownerClientId);

        // Checkout offers nothing a fresh migration has not switched on, so the method the order
        // names has to be enabled first, exactly as an operator would.
        var enable = await admin.PutAsJsonAsync(
            "/api/admin/integrations/bank-transfer",
            new { isEnabled = true, config = new Dictionary<string, string> { ["iban"] = BankTransferTestSeeds.Iban } });
        enable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // The renew order needs an active domain product to hang its line off; a fresh test
        // database has none, and without it the route answers 503 before the order is placed.
        if (!await context.Products.AnyAsync(
                p => p.Status == ProductStatus.Active && p.Type == ProductType.Domain, CancellationToken.None))
        {
            var group = ProductGroup.Create($"domains-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Products.Add(Product.Create(
                group.Id, "Domain Registration", null, null, null, null, ProductType.Domain, 0m, 0m, DateTimeOffset.UtcNow));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        // The order is priced from the TLD table's renewal column; a fresh database has no
        // ".com" row, which would refuse the order as unpriced.
        if (!await context.TldConfigs.AnyAsync(t => t.Tld == "com", CancellationToken.None))
        {
            var config = TldConfig.Create("com", RegistrarModule.NameAm, "USD", "USD", true, 0, []);
            config.UpdateSellPrices([], [], new() { [1] = 12m });
            context.TldConfigs.Add(config);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return new(owner, ownerClientId, domainId, otherDomainId);
    }

    /// <summary>Seeds an invoice for the client through a fresh scope, shaped by <paramref name="build"/>.</summary>
    /// <param name="clientId">The invoiced client.</param>
    /// <param name="build">Adds lines and credit to the new invoice; runs before the first save.</param>
    /// <param name="after">Changes made after the first save (a status change needs the id), or null.</param>
    /// <param name="clientCredit">Credit to move from the client's balance onto the invoice, as the renewal raiser does; 0 for none.</param>
    /// <returns>The invoice id.</returns>
    private async Task<int> SeedInvoiceAsync(int clientId, Action<Invoice> build, Action<Invoice>? after = null, decimal clientCredit = 0m)
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
        if (after is not null)
        {
            after(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return invoice.Id;
    }

    /// <summary>Adds a renewal line for <paramref name="domainId"/> starting in ten days.</summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="domainId">The domain.</param>
    private static void AddRenewal(Invoice invoice, int domainId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(10);
        invoice.AddDomainRenewal("Domain renewal", 12m, domainId, start, start.AddYears(1));
    }

    /// <summary>Places the owner's renew order for the domain.</summary>
    /// <param name="owner">The authenticated owner.</param>
    /// <param name="domainId">The domain.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> RenewAsync(HttpClient owner, int domainId) =>
        owner.PostAsJsonAsync($"/api/me/domains/{domainId}/renew", new { years = 1, paymentMethod = "bank_transfer" });

    /// <summary>Reloads an invoice and its lines, untracked, from a fresh scope.</summary>
    /// <param name="id">The invoice id.</param>
    /// <returns>The invoice as stored.</returns>
    private async Task<Invoice> LoadInvoiceAsync(int id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Invoices.AsNoTracking().Include(i => i.Items).FirstAsync(i => i.Id == id, CancellationToken.None);
    }

    /// <summary>Reads a client's credit balance from a fresh scope.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The balance as stored.</returns>
    private async Task<decimal> CreditOfAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => c.CreditBalance).FirstAsync(CancellationToken.None);
    }

    /// <summary>
    /// A client renew order cancels the domain's open auto-renewal invoice, so the same year is
    /// never billed twice, returns the credit that invoice had applied to the client's balance,
    /// and still places the order. Fails if the handler leaves the renewal invoice Unpaid, keeps
    /// the credit on the cancelled invoice, or fails to place the order.
    /// </summary>
    [Fact]
    public async Task RenewMyDomain_CancelsTheOpenAutoRenewalInvoiceAndReturnsItsCreditAsync()
    {
        var (owner, clientId, domainId, _) = await ArrangeAsync("renew-my-domain-cancel");
        var invoiceId = await SeedInvoiceAsync(clientId, i => AddRenewal(i, domainId), clientCredit: 5m);
        (await CreditOfAsync(clientId)).Should().Be(0m);

        var response = await RenewAsync(owner, domainId);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Cancelled);
        reloaded.Credit.Should().Be(0m);
        (await CreditOfAsync(clientId)).Should().Be(5m);
    }

    /// <summary>A paid renewal invoice for the domain is left as it is; the order is still placed.</summary>
    [Fact]
    public async Task RenewMyDomain_LeavesAPaidRenewalInvoiceAloneAsync()
    {
        var (owner, clientId, domainId, _) = await ArrangeAsync("renew-my-domain-paid");
        var invoiceId = await SeedInvoiceAsync(clientId, i => AddRenewal(i, domainId), after: i => i.RecordPaidHistory("t", DateTimeOffset.UtcNow));

        var response = await RenewAsync(owner, domainId);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Paid);
        reloaded.Items.Should().ContainSingle();
    }

    /// <summary>Another domain's open renewal invoice is not touched by this domain's renew order.</summary>
    [Fact]
    public async Task RenewMyDomain_LeavesAnotherDomainsInvoiceAloneAsync()
    {
        var (owner, clientId, domainId, otherDomainId) = await ArrangeAsync("renew-my-domain-other");
        var invoiceId = await SeedInvoiceAsync(clientId, i => AddRenewal(i, otherDomainId));

        var response = await RenewAsync(owner, domainId);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Unpaid);
        reloaded.Items.Should().ContainSingle(l => l.RegisteredDomainId == otherDomainId);
    }

    /// <summary>
    /// An unpaid invoice that bills this domain with another domain keeps the other line: only this
    /// domain's line is removed, the total drops, and the credit that now exceeds it goes back to
    /// the client.
    /// </summary>
    [Fact]
    public async Task RenewMyDomain_MixedUnpaidInvoice_RemovesOnlyThisDomainsLineAsync()
    {
        var (owner, clientId, domainId, otherDomainId) = await ArrangeAsync("renew-my-domain-mixed");

        // 12 + 12 billed, 20 of credit applied: after this domain's 12 goes, 8 of credit is excess.
        var invoiceId = await SeedInvoiceAsync(
            clientId, i => { AddRenewal(i, domainId); AddRenewal(i, otherDomainId); }, clientCredit: 20m);

        var response = await RenewAsync(owner, domainId);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Unpaid);
        reloaded.Items.Should().ContainSingle().Which.RegisteredDomainId.Should().Be(otherDomainId);
        reloaded.Credit.Should().Be(12m);
        reloaded.Total.Should().Be(0m);
        (await CreditOfAsync(clientId)).Should().Be(8m);
    }

    /// <summary>
    /// An overdue invoice that bills this domain with something else cannot be split, so the renew
    /// order is refused with 409 <c>RENEWAL_INVOICE_OPEN</c> and the invoice is left exactly as it was.
    /// </summary>
    [Fact]
    public async Task RenewMyDomain_MixedOverdueInvoice_Returns409Async()
    {
        var (owner, clientId, domainId, _) = await ArrangeAsync("renew-my-domain-overdue");
        var invoiceId = await SeedInvoiceAsync(
            clientId, i => { AddRenewal(i, domainId); i.AddItem("Hosting", 10m, 1); }, after: i => i.MarkOverdue(DateTimeOffset.UtcNow));

        var response = await RenewAsync(owner, domainId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.Overdue);
        reloaded.Items.Should().HaveCount(2);
    }

    /// <summary>
    /// A renewal invoice in PaymentPending (a gateway session may be in flight) is never cancelled,
    /// even when it bills only this domain: the renew order is refused with 409
    /// <c>RENEWAL_INVOICE_OPEN</c> and the invoice is left as it was. No domain method reaches
    /// PaymentPending today, so the status is written directly.
    /// </summary>
    [Fact]
    public async Task RenewMyDomain_PaymentPendingInvoice_Returns409Async()
    {
        var (owner, clientId, domainId, _) = await ArrangeAsync("renew-my-domain-pending");
        var invoiceId = await SeedInvoiceAsync(clientId, i => AddRenewal(i, domainId));
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.Invoices.Where(i => i.Id == invoiceId)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.Status, InvoiceStatus.PaymentPending), CancellationToken.None);
        }

        var response = await RenewAsync(owner, domainId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.RENEWAL_INVOICE_OPEN);
        var reloaded = await LoadInvoiceAsync(invoiceId);
        reloaded.Status.Should().Be(InvoiceStatus.PaymentPending);
        reloaded.Items.Should().ContainSingle();
    }

    /// <summary>
    /// When the open auto-renewal invoice was released but the order could not be placed (here an
    /// unknown payment method), the domain is not left without a renewal: the next renewal run
    /// raises a fresh invoice for it. Fails if the cancelled line still blocks its period.
    /// </summary>
    [Fact]
    public async Task RenewMyDomain_ReleasedButOrderFailed_NextRenewalRunRaisesANewInvoiceAsync()
    {
        var (owner, clientId, domainId, _) = await ArrangeAsync("renew-my-domain-reraise");
        using (var arrange = factory.Services.CreateScope())
        {
            // Due inside the renewal lead time and auto-renewing, so the renewal run bills it.
            var context = arrange.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.Domains.Where(d => d.Id == domainId)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(d => d.AutoRenew, true)
                        .SetProperty(d => d.ExpiresAt, DateTimeOffset.UtcNow.AddDays(10)),
                    CancellationToken.None);
        }

        var invoiceId = await SeedInvoiceAsync(clientId, i => AddRenewal(i, domainId));

        var response = await owner.PostAsJsonAsync(
            $"/api/me/domains/{domainId}/renew", new { years = 1, paymentMethod = "no-such-method" });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await LoadInvoiceAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Cancelled);

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var raised = await bus.InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(clientId), CancellationToken.None);

        raised.IsSuccess.Should().BeTrue();
        var readContext = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var fresh = await readContext.Invoices.AsNoTracking().Include(i => i.Items)
            .Where(i => i.ClientId == clientId && i.Id != invoiceId && i.Status == InvoiceStatus.Unpaid)
            .ToListAsync(CancellationToken.None);
        fresh.Should().ContainSingle(i => i.Items.Any(l => l.RegisteredDomainId == domainId));
    }

    /// <summary>A period outside one to ten years is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    [Fact]
    public async Task RenewMyDomain_YearsOutOfRange_Returns400ValidationErrorAsync()
    {
        var owner = await DomainsTestSeeds.RegisterOwnerAsync(factory, "renew-my-domain-invalid");

        var response = await owner.Client.PostAsJsonAsync(
            $"/api/me/domains/{DomainsTestSeeds.UnknownId}/renew", new { years = 0, paymentMethod = "bank-gateway" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
