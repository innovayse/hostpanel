namespace Innovayse.Application.Tests.Clients;

using Innovayse.Application.Auth.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Moq;
using Xunit;

/// <summary>Once a client has been billed, their currency cannot be changed through the admin form.</summary>
/// <remarks>
/// The WHMCS failure this prevents: changing the code re-labels every past invoice without
/// converting a single amount, so $100 reads as ֏100. A client with any invoice keeps their
/// currency; one with none may still be moved, because nothing exists to be re-labelled.
/// </remarks>
public sealed class UpdateClientCurrencyLockTests
{
    /// <summary>The subject the fixture client is linked to.</summary>
    private const string Subject = "user-16";

    /// <summary>The fixture client's id.</summary>
    private const int ClientId = 16;

    /// <summary>The address the account signs in with; posted back unchanged on every save.</summary>
    private const string CurrentEmail = "anahitakv@example.com";

    /// <summary>The currency the fixture client starts out billed in.</summary>
    private const string OriginalCurrency = "USD";

    /// <summary>The currencies this panel has configured.</summary>
    private readonly Mock<ICurrencyRepository> currencies = new();

    /// <summary>The client's billing history, answered per test.</summary>
    private readonly Mock<IInvoiceRepository> invoices = new();

    /// <summary>The client under edit, billed in <see cref="OriginalCurrency"/>.</summary>
    private readonly Client client;

    /// <summary>Configures USD (base) and AMD as enabled, and EUR as configured but switched off.</summary>
    public UpdateClientCurrencyLockTests()
    {
        client = Client.Create(Subject, "Roots", "Agency", CurrentEmail);
        client.SetCurrency(OriginalCurrency);

        var usd = Currency.Create("USD", "840", "$", string.Empty, 2, 1m, isBase: true);
        var amd = Currency.Create("AMD", "051", string.Empty, " ֏", 0, 0.0025m, isBase: false);
        var eur = Currency.Create("EUR", "978", "€", string.Empty, 2, 1.1m, isBase: false);
        eur.Disable();

        currencies.Setup(c => c.FindAsync("USD", It.IsAny<CancellationToken>())).ReturnsAsync(usd);
        currencies.Setup(c => c.FindAsync("AMD", It.IsAny<CancellationToken>())).ReturnsAsync(amd);
        currencies.Setup(c => c.FindAsync("EUR", It.IsAny<CancellationToken>())).ReturnsAsync(eur);
        currencies.Setup(c => c.FindAsync("XXX", It.IsAny<CancellationToken>())).ReturnsAsync((Currency?)null);
    }

    /// <summary>The command the account form posts, with every field populated and the currency chosen.</summary>
    /// <param name="currency">The currency the form posts.</param>
    /// <returns>A command ready to hand to the handler.</returns>
    private static UpdateClientCommand Command(string? currency) => new(
        ClientId: ClientId,
        Email: CurrentEmail,
        FirstName: "Roots",
        LastName: "Agency",
        CompanyName: null,
        Phone: null,
        Street: null,
        Address2: null,
        City: null,
        State: null,
        PostCode: null,
        Country: null,
        Language: null,
        Currency: currency,
        PaymentMethod: null,
        BillingContact: null,
        AdminNotes: null,
        NotifyGeneral: true,
        NotifyInvoice: true,
        NotifySupport: true,
        NotifyProduct: true,
        NotifyDomain: true,
        NotifyAffiliate: true,
        LateFees: true,
        OverdueNotices: true,
        TaxExempt: false,
        SeparateInvoices: false,
        DisableCcProcessing: false,
        MarketingOptIn: false,
        StatusUpdate: true,
        AllowSso: true,
        Status: null);

    /// <summary>Assembles the handler over the fixture client and the given billing history.</summary>
    /// <param name="invoiced">Whether the client already has at least one invoice.</param>
    /// <returns>The handler under test.</returns>
    private UpdateClientHandler Handler(bool invoiced)
    {
        var repo = new Mock<IClientRepository>();
        repo.Setup(r => r.FindByIdAsync(ClientId, It.IsAny<CancellationToken>())).ReturnsAsync(client);

        invoices.Setup(i => i.AnyForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoiced);

        var identity = new Mock<IIdentityProvider>();
        identity.Setup(i => i.FindBySubjectAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdentityAccount(Subject, CurrentEmail, "Roots", "Agency"));

        return new UpdateClientHandler(
            repo.Object,
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IUserProvisioning>(),
            identity.Object,
            invoices.Object,
            currencies.Object);
    }

    /// <summary>A client with an invoice keeps their currency, and the refusal says why.</summary>
    [Fact]
    public async Task Handle_RefusesACurrencyChangeOnceInvoiced()
    {
        var handler = Handler(invoiced: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(Command("AMD"), CancellationToken.None));

        Assert.Contains("already been invoiced", ex.Message, StringComparison.Ordinal);
        Assert.Equal(OriginalCurrency, client.Currency);
    }

    /// <summary>A client with no invoices may still be moved.</summary>
    [Fact]
    public async Task Handle_AllowsACurrencyChangeBeforeAnyInvoice()
    {
        var handler = Handler(invoiced: false);

        await handler.HandleAsync(Command("AMD"), CancellationToken.None);

        Assert.Equal("AMD", client.Currency);
    }

    /// <summary>
    /// Posting the same currency back is not a change, so an invoiced client's form still saves.
    /// The form posts the field on every save; casing differs on purpose.
    /// </summary>
    [Fact]
    public async Task Handle_SameCurrencyOnAnInvoicedClientIsNotAChange()
    {
        var handler = Handler(invoiced: true);

        await handler.HandleAsync(Command("usd"), CancellationToken.None);

        Assert.Equal(OriginalCurrency, client.Currency);
        invoices.Verify(i => i.AnyForClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A code this panel has not configured is refused, and the refusal names it.</summary>
    [Fact]
    public async Task Handle_RefusesAnUnconfiguredCurrency()
    {
        var handler = Handler(invoiced: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(Command("XXX"), CancellationToken.None));

        Assert.Contains("XXX", ex.Message, StringComparison.Ordinal);
        Assert.Equal(OriginalCurrency, client.Currency);
    }

    /// <summary>A configured currency the operator has switched off is refused too; it is not on offer.</summary>
    [Fact]
    public async Task Handle_RefusesADisabledCurrency()
    {
        var handler = Handler(invoiced: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(Command("EUR"), CancellationToken.None));

        Assert.Contains("EUR", ex.Message, StringComparison.Ordinal);
        Assert.Equal(OriginalCurrency, client.Currency);
    }

    /// <summary>A form that posts no currency leaves the recorded one alone.</summary>
    [Fact]
    public async Task Handle_NullCurrencyKeepsTheCurrentOne()
    {
        var handler = Handler(invoiced: true);

        await handler.HandleAsync(Command(null), CancellationToken.None);

        Assert.Equal(OriginalCurrency, client.Currency);
    }
}
