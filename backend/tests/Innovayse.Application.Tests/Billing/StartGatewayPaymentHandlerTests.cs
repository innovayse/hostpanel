namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Billing.Options;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
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
    private readonly Mock<IClientRepository> clientRepo = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<IPaymentPlugin> plugin = new();
    private readonly Mock<IUnitOfWork> uow = new();

    public StartGatewayPaymentHandlerTests()
    {
        // Matches the default (null client currency → BillingOptions left at its own AMD default
        // → "051") currency path used by most tests below.
        plugin.SetupGet(p => p.CurrencyCode).Returns("051");
    }

    private StartGatewayPaymentHandler CreateHandler(
        IOptions<BillingOptions>? billingOptions = null, ILogger<StartGatewayPaymentHandler>? logger = null) =>
        new(invoiceRepo.Object, clientRepo.Object, resolver.Object, uow.Object,
            billingOptions ?? Options.Create(new BillingOptions()),
            AllowedOriginOptions(),
            logger ?? NullLogger<StartGatewayPaymentHandler>.Instance);

    /// <summary>The return-url origins every test runs with, matching <see cref="ReturnUrl"/>.</summary>
    private static IOptions<GatewayReturnUrlOptions> AllowedOriginOptions(params string[] origins) =>
        Options.Create(new GatewayReturnUrlOptions
        {
            AllowedOrigins = origins.Length > 0 ? origins : ["https://portal"],
        });

    /// <summary>Builds billing options with an explicit default currency, to prove that value
    /// overrides the option class's own AMD default.</summary>
    private static IOptions<BillingOptions> BillingOptionsWithDefaultCurrency(string defaultCurrency) =>
        Options.Create(new BillingOptions { DefaultCurrency = defaultCurrency });

    private Invoice CreateInvoice(decimal total = 25.50m, string? clientCurrency = null, int? id = null)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        if (id is not null)
        {
            // Applied before the repo Setup below so the mock is wired to the id the test
            // actually wants (e.g. int.MaxValue), not the auto-assigned default of 0.
            SetInvoiceId(invoice, id.Value);
        }

        invoice.AddItem("Hosting", total, 1);
        invoiceRepo.Setup(r => r.FindByIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        if (clientCurrency is not null)
        {
            var client = Client.Create("user-1", "Jane", "Doe", "jane@example.com");
            client.UpdatePreferences(clientCurrency, null, null, null);
            clientRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(client);
        }
        else
        {
            clientRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Client?)null);
        }

        return invoice;
    }

    [Fact]
    public async Task HandleAsync_RegistersPaymentAndStoresSession()
    {
        var invoice = CreateInvoice(total: 25.50m);
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        PaymentRequest? sent = null;
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new PaymentSession("gw-55", "https://pg/pay?mdOrder=gw-55"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-55", redirect);
        Assert.Equal("gw-55", invoice.GatewayOrderId);
        Assert.Equal("innovayse-inecobank", invoice.GatewayModule);
        Assert.NotNull(sent);
        Assert.Equal(2550, sent!.AmountMinor); // 25.50 major → 2550 minor units
        Assert.StartsWith($"INV{invoice.Id}-", sent.OrderNumber);
        Assert.True(sent.OrderNumber.Length <= 25);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AmdClientAgainstAmdPlugin_RegistersNormally()
    {
        var invoice = CreateInvoice(total: 10m, clientCurrency: "AMD");
        plugin.SetupGet(p => p.CurrencyCode).Returns("051");
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-amd", "https://pg/pay?mdOrder=gw-amd"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-amd", redirect);
    }

    [Fact]
    public async Task HandleAsync_UsdClientAgainstAmdPlugin_RefusesWithoutCallingGateway()
    {
        var invoice = CreateInvoice(total: 25m, clientCurrency: "USD");
        plugin.SetupGet(p => p.CurrencyCode).Returns("051"); // gateway configured for AMD
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("USD", ex.Message);
        Assert.Contains("051", ex.Message);
        plugin.Verify(
            p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NullClientCurrencyWithNoConfiguredDefault_TreatedAsAmd()
    {
        // CreateInvoice with clientCurrency: null → clientRepo.FindByIdAsync returns null (unset mock).
        // CreateHandler() below leaves BillingOptions at its own default, so the handler must
        // bill in AMD.
        var invoice = CreateInvoice(total: 25m);
        plugin.SetupGet(p => p.CurrencyCode).Returns("051"); // AMD
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-amd-default", "https://pg/pay?mdOrder=gw-amd-default"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-amd-default", redirect);
    }

    [Fact]
    public async Task HandleAsync_NullClientCurrencyWithConfiguredDefault_UsesConfiguredValue()
    {
        // An explicit Billing:DefaultCurrency must win over the option class's AMD default.
        var invoice = CreateInvoice(total: 25m);
        plugin.SetupGet(p => p.CurrencyCode).Returns("978"); // EUR — matches the configured default below
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-eur-config", "https://pg/pay?mdOrder=gw-eur-config"));

        var redirect = await CreateHandler(BillingOptionsWithDefaultCurrency("EUR")).HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-eur-config", redirect);
    }

    [Fact]
    public async Task HandleAsync_UnmappableClientCurrency_Refuses()
    {
        var invoice = CreateInvoice(total: 25m, clientCurrency: "XYZ");
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None));

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
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_InvoiceAlreadyPaid_RefusesWithoutResolvingPluginOrCallingGateway()
    {
        var invoice = CreateInvoice();
        invoice.MarkPaid("earlier-txn");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
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
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_ReturnUrlOriginNotAllowed_Refuses()
    {
        var invoice = CreateInvoice();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", "https://evil.example/steal"),
            CancellationToken.None));

        Assert.Contains("evil.example", ex.Message);
        resolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FreshLiveSession_RefusesUntilItIsKnownDeclined()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("innovayse-inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Pending, null, "orderStatus:0"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None));

        Assert.Contains("already in progress", ex.Message);
        plugin.Verify(
            p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FreshLiveSessionAlreadyDeclined_ReplacesIt()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("innovayse-inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Declined, null, "orderStatus:6"));
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-fresh", "https://pg/pay?mdOrder=gw-fresh"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None);

        Assert.Equal("https://pg/pay?mdOrder=gw-fresh", redirect);
        Assert.Equal("gw-fresh", invoice.GatewayOrderId);
    }

    [Fact]
    public async Task HandleAsync_SessionOlderThanWindow_IsReplacedWithoutCheckingStatus()
    {
        var invoice = CreateInvoice();
        invoice.SetGatewaySession("innovayse-inecobank", "gw-old");
        SetGatewayStartedAt(invoice, DateTimeOffset.UtcNow.AddMinutes(-21));
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentSession("gw-new", "https://pg/pay?mdOrder=gw-new"));

        var redirect = await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
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
    /// can exercise id values — like <see cref="int.MaxValue"/> — that <see cref="Invoice.Create(int, DateTimeOffset)"/>
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
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        PaymentRequest? sent = null;
        plugin.Setup(p => p.CreatePaymentAsync(It.IsAny<PaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentRequest, CancellationToken>((r, _) => sent = r)
            .ReturnsAsync(new PaymentSession("gw-55", "https://pg/pay?mdOrder=gw-55"));

        await CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
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
        invoice.SetGatewaySession("innovayse-inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gateway status API unreachable"));
        var logger = new Mock<ILogger<StartGatewayPaymentHandler>>();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(logger: logger.Object).HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
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
        invoice.SetGatewaySession("innovayse-inecobank", "gw-stale");
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        plugin.Setup(p => p.GetStatusAsync("gw-stale", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", ReturnUrl),
            CancellationToken.None));
    }
}
