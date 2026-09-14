namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.PayInvoice;
using Innovayse.Application.Common;
using Innovayse.Domain.Audit.Interfaces;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// An invoice is charged in the currency it was issued in. The command used to carry a
/// caller-chosen code defaulting to USD, which charged an AMD invoice's total as dollars.
/// </summary>
public sealed class PayInvoiceHandlerTests
{
    /// <summary>The invoice under test.</summary>
    private const int InvoiceId = 41;

    [Fact]
    public async Task Handle_ChargesInTheInvoicesOwnCurrency()
    {
        var invoice = Invoice.Create(5, DateTimeOffset.UtcNow.AddDays(7), "AMD");
        invoice.AddItem("Starter", 1200m, 1);
        var repo = new Mock<IInvoiceRepository>();
        repo.Setup(r => r.FindByIdAsync(InvoiceId, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        ChargeRequest? charged = null;
        var gateway = new Mock<IPaymentGateway>();
        gateway.Setup(g => g.ChargeAsync(It.IsAny<ChargeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChargeRequest, CancellationToken>((r, _) => charged = r)
            .ReturnsAsync(new PaymentResult(true, "tx-1", null));
        var handler = new PayInvoiceHandler(
            repo.Object, gateway.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IActivityLogRepository>(),
            Mock.Of<ICurrentRequestContext>());

        await handler.HandleAsync(new PayInvoiceCommand(InvoiceId), CancellationToken.None);

        Assert.NotNull(charged);
        Assert.Equal("AMD", charged!.Currency);
        Assert.Equal(1200m, charged.Amount);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }
}
