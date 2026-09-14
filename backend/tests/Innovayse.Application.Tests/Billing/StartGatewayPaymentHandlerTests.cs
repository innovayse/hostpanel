namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Options;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Common;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="StartGatewayPaymentHandler"/>.</summary>
public class StartGatewayPaymentHandlerTests
{
    private const string ReturnUrl = "https://portal/payment/result?invoice=1";

    private readonly Mock<IInvoiceRepository> invoiceRepo = new();
    private readonly Mock<ICurrencyRepository> currencies = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<IPaymentPlugin> plugin = new();
    private readonly Mock<IUnitOfWork> uow = new();

    public StartGatewayPaymentHandlerTests()
    {
        // The configured currencies every test runs against. The fixture invoice bills in USD
        // and the plugin charges in USD (840) unless a test says otherwise, so the default path
        // starts normally.
        ConfiguredCurrency("AMD", "051");
        ConfiguredCurrency("USD", "840");
        plugin.SetupGet(p => p.CurrencyCode).Returns("840");
    }

    private StartGatewayPaymentHandler CreateHandler(ILogger<StartGatewayPaymentHandler>? logger = null) =>
        new(invoiceRepo.Object, currencies.Object, resolver.Object, uow.Object,
            AllowedOriginOptions(),
            logger ?? NullLogger<StartGatewayPaymentHandler>.Instance);

    /// <summary>Makes <paramref name="code"/> a configured currency with the given ISO numeric.</summary>
    private void ConfiguredCurrency(string code, string numeric) =>
        currencies.Setup(c => c.FindAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Currency.Create(code, numeric, string.Empty, string.Empty, 2, 1m, isBase: false));

    /// <summary>The return-url origins every test runs with, matching <see cref="ReturnUrl"/>.</summary>
    private static IOptions<GatewayReturnUrlOptions> AllowedOriginOptions(params string[] origins) =>
        Options.Create(new GatewayReturnUrlOptions
        {
            AllowedOrigins = origins.Length > 0 ? origins : ["https://portal"],
        });

    /// <summary>Builds an unpaid invoice in <paramref name="currency"/> and wires the repository to hand it back.</summary>
    private Invoice CreateInvoice(decimal total = 25.50m, string currency = "USD", int? id = null)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14), currency: currency);
        if (id is not null)
        {
            // Applied before the repo Setup below so the mock is wired to the id the test
            // actually wants (e.g. int.MaxValue), not the auto-assigned default of 0.
            SetInvoiceId(invoice, id.Value);
        }

        invoice.AddItem("Hosting", total, 1);
        invoiceRepo.Setup(r => r.FindByIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        return invoice;
    }

    [Fact]
    public async Task HandleAsync_RegistersPaymentAndStoresSession()
    {
        var invoice = CreateInvoice(total: 25.50m);
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        PaymentRequest? sent = null;
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new PaymentSession("gw-55", "https://pg/pay?mdOrder=gw-55"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-55", redirect);
        Assert.Equal("gw-55", invoice.GatewayOrderId);
        Assert.Equal("inecobank", invoice.GatewayModule);
        Assert.NotNull(sent);
        Assert.Equal(2550, sent!.AmountMinor); // 25.50 major → 2550 minor units
        Assert.StartsWith($"INV{invoice.Id}-", sent.OrderNumber);
        Assert.True(sent.OrderNumber.Length <= 25);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AmdInvoiceAgainstAmdPlugin_RegistersNormally()
    {
        var invoice = CreateInvoice(total: 10m, currency: "AMD");
        plugin.SetupGet(p => p.CurrencyCode).Returns("051");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-amd", "https://pg/pay?mdOrder=gw-amd"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-amd", redirect);
    }

    [Fact]
    public async Task HandleAsync_UsdInvoiceAgainstAmdPlugin_RefusesWithoutCallingGateway()
    {
        var invoice = CreateInvoice(total: 25m, currency: "USD");
        plugin.SetupGet(p => p.CurrencyCode).Returns("051"); // gateway configured for AMD
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("USD", ex.Message);
        Assert.Contains("051", ex.Message);
        plugin.Verify(
            p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The invoice's own currency decides the match, not the client's current one. A client
    /// whose currency was changed after an invoice was raised still pays that invoice in what it
    /// was raised in.
    /// </summary>
    [Fact]
    public async Task Handle_ComparesThePluginToTheInvoiceCurrency_NotTheClients()
    {
        var invoice = CreateInvoice(total: 25.50m, currency: "AMD");
        plugin.SetupGet(p => p.CurrencyCode).Returns("051");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-amd", "https://pg/pay?mdOrder=gw-amd"));

        // The handler never asks who the client is or what they are billed in today: the only
        // currency lookup it may make is the invoice's own code. A payer resolver is deliberately
        // absent from the constructor, so "the client record now says USD" cannot reach it.
        var url = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl), CancellationToken.None);

        Assert.NotNull(url);
        currencies.Verify(c => c.FindAsync("AMD", It.IsAny<CancellationToken>()), Times.Once);
        currencies.Verify(c => c.FindAsync(It.Is<string>(s => s != "AMD"), It.IsAny<CancellationToken>()), Times.Never);
        currencies.Verify(c => c.GetBaseAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An invoice in a currency the panel no longer has configured cannot be paid through a gateway.</summary>
    [Fact]
    public async Task HandleAsync_InvoiceCurrencyNotConfigured_Refuses()
    {
        var invoice = CreateInvoice(total: 25m, currency: "XYZ");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("XYZ", ex.Message);
        plugin.Verify(
            p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ModuleUnavailable_Throws()
    {
        var invoice = CreateInvoice();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_InvoiceAlreadyPaid_RefusesWithoutResolvingPluginOrCallingGateway()
    {
        var invoice = CreateInvoice();
        invoice.MarkPaid("earlier-txn");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("Paid", ex.Message);
        resolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InvoiceCancelled_Refuses()
    {
        var invoice = CreateInvoice();
        invoice.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ReturnUrlOriginNotAllowed_Refuses()
    {
        var invoice = CreateInvoice();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", "https://evil.example/steal"),
            CancellationToken.None));

        Assert.Contains("evil.example", ex.Message);
        resolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FreshLiveSession_RefusesUntilItIsKnownDeclined()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Pending, null, "orderStatus:0"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("already in progress", ex.Message);
        plugin.Verify(
            p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FreshLiveSessionAlreadyDeclined_ReplacesIt()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Declined, null, "orderStatus:6"));
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-fresh", "https://pg/pay?mdOrder=gw-fresh"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-fresh", redirect);
        Assert.Equal("gw-fresh", invoice.GatewayOrderId);
    }

    [Fact]
    public async Task HandleAsync_SessionOlderThanWindow_IsReplacedWithoutCheckingStatus()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("inecobank", "gw-old");
        SetGatewayStartedAt(invoice, DateTimeOffset.UtcNow.AddMinutes(-21));
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-new", "https://pg/pay?mdOrder=gw-new"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-new", redirect);
        plugin.Verify(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Backdates <see cref="Invoice.GatewayStartedAt"/> via reflection to simulate an old session
    /// without depending on real elapsed time in the test.</summary>
    private static void SetGatewayStartedAt(Invoice invoice, DateTimeOffset startedAt)
    {
        var backingField = typeof(Invoice).GetField(
            $"<{nameof(Invoice.GatewayStartedAt)}>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        backingField.SetValue(invoice, startedAt);
    }

    /// <summary>Overrides <see cref="Entity.Id"/> via reflection (its setter is private) so tests
    /// can exercise id values — like <see cref="int.MaxValue"/> — that <see cref="Invoice.Create(int, DateTimeOffset, string)"/>
    /// never produces on its own.</summary>
    private static void SetInvoiceId(Invoice invoice, int id)
    {
        var backingField = typeof(Entity).GetField(
            "<Id>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        backingField.SetValue(invoice, id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public async Task HandleAsync_OrderNumberNeverExceedsGatewayLimit_EvenForALargeInvoiceId(int invoiceId)
    {
        // The fixture default (invoice.Id == 0) never approaches the gateway's 25-char
        // orderNumber limit, so a regression that widened the format would go unnoticed.
        // int.MaxValue (10 digits) is the largest an `int` invoice id can ever be.
        var invoice = CreateInvoice(total: 25.50m, id: invoiceId);
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        PaymentRequest? sent = null;
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new PaymentSession("gw-55", "https://pg/pay?mdOrder=gw-55"));

        await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.NotNull(sent);
        Assert.StartsWith($"INV{invoiceId}-", sent!.OrderNumber);
        Assert.True(
            sent.OrderNumber.Length <= 25,
            $"Order number '{sent.OrderNumber}' is {sent.OrderNumber.Length} chars; the gateway rejects orderNumber over 25 chars.");
    }

    [Fact]
    public async Task HandleAsync_LiveSessionStatusCheckThrows_RefusesAsStillLiveAndLogsWarning()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gateway status API unreachable"));
        var logger = new Mock<ILogger<StartGatewayPaymentHandler>>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(logger: logger.Object).HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("already in progress", ex.Message);
        plugin.Verify(
            p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LiveSessionStatusCheckCancelled_PropagatesRatherThanRefusing()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "inecobank", ReturnUrl),
            CancellationToken.None));
    }
}
