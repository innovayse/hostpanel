namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.StartGatewayPayment;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.SDK.Plugins;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="StartGatewayPaymentHandler"/>.</summary>
public class StartGatewayPaymentHandlerTests
{
    private readonly Mock<IInvoiceRepository> invoiceRepo = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<IPaymentPlugin> plugin = new();
    private readonly Mock<IUnitOfWork> uow = new();

    private StartGatewayPaymentHandler CreateHandler() =>
        new(invoiceRepo.Object, resolver.Object, uow.Object);

    private Invoice CreateInvoice(decimal total = 25.50m)
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.AddItem("Hosting", total, 1);
        invoiceRepo.Setup(r => r.FindByIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
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
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", "https://portal/payment/result?invoice=1"),
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
    public async Task HandleAsync_ModuleUnavailable_Throws()
    {
        var invoice = CreateInvoice();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(
            new StartGatewayPaymentCommand(invoice.Id, "innovayse-inecobank", "https://portal/r"),
            CancellationToken.None));
    }
}
