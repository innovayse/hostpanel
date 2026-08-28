namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.CompleteMyGatewayPayment;
using Innovayse.Application.Billing.Commands.PayInvoice;
using Innovayse.Application.Billing.Commands.PayMyInvoice;
using Innovayse.Application.Billing.Commands.StartMyGatewayPayment;
using Innovayse.Application.Billing.Common;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetMyInvoice;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Moq;
using Wolverine;
using Xunit;

/// <summary>
/// Proves the client-facing invoice routes are scoped to the caller.
/// <para>
/// The rule used to be four copies of <c>if (invoice.ClientId != profile.Id) return Forbid();</c>
/// in <c>MyBillingController</c> -- correct, but written at the endpoint, so it travelled
/// separately from the command. These tests assert it where it now lives: inside the handlers,
/// and answering a stranger's invoice exactly as it answers one that does not exist.
/// </para>
/// </summary>
public sealed class MyInvoiceOwnershipTests
{
    /// <summary>Identity subject of the caller in every test below.</summary>
    private const string CallerSubject = "user-caller";

    /// <summary>A client id that is deliberately not the caller's, so ownership must not match.</summary>
    private const int StrangersClientId = 4242;

    /// <summary>The invoice id every probe asks for.</summary>
    private const int InvoiceId = 11;

    /// <summary>Builds the caller's own client record. Its <c>Id</c> is 0, the unsaved default.</summary>
    /// <returns>A client owned by <see cref="CallerSubject"/>.</returns>
    private static Client CallerClient() => Client.Create(CallerSubject, "Jane", "Doe", "jane@example.com");

    /// <summary>
    /// Builds an <see cref="IInvoiceOwnership"/> over the given world.
    /// </summary>
    /// <param name="client">What the client repository answers for the caller's subject.</param>
    /// <param name="invoice">What the invoice repository answers for <see cref="InvoiceId"/>.</param>
    /// <returns>The rule under test.</returns>
    private static IInvoiceOwnership OwnershipOver(Client? client, Invoice? invoice)
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByUserIdAsync(CallerSubject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.FindByIdAsync(InvoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var caller = new Mock<ICurrentRequestContext>();
        caller.Setup(c => c.RequireUserId()).Returns(CallerSubject);

        return new InvoiceOwnership(invoices.Object, clients.Object, caller.Object);
    }

    /// <summary>An ownership rule that refuses everything, for the handler tests.</summary>
    /// <returns>A rule that throws <see cref="InvoiceNotFoundException"/>.</returns>
    private static IInvoiceOwnership RefusingOwnership() =>
        OwnershipOver(CallerClient(), Invoice.Create(StrangersClientId, DateTimeOffset.UtcNow.AddDays(7)));

    /// <summary>An ownership rule that accepts, for the handler tests.</summary>
    /// <returns>A rule that completes.</returns>
    private static IInvoiceOwnership AcceptingOwnership() =>
        OwnershipOver(CallerClient(), Invoice.Create(clientId: 0, DateTimeOffset.UtcNow.AddDays(7)));

    /// <summary>The whole point: an invoice belonging to another client is not readable.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenInvoiceBelongsToAnotherClient_RefusesAsync()
    {
        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => RefusingOwnership().RequireOwnedByCallerAsync(InvoiceId, CancellationToken.None));
    }

    /// <summary>
    /// An invoice that does not exist answers exactly as one that is somebody else's, and so does
    /// a caller with no client record. All three must be indistinguishable, or the sequential ids
    /// can be walked to find out which are real.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_MissingAndStrangersAndNoProfile_AnswerAlikeAsync()
    {
        var strangers = await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => RefusingOwnership().RequireOwnedByCallerAsync(InvoiceId, CancellationToken.None));

        var missing = await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => OwnershipOver(CallerClient(), invoice: null)
                .RequireOwnedByCallerAsync(InvoiceId, CancellationToken.None));

        var noProfile = await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => OwnershipOver(client: null, Invoice.Create(StrangersClientId, DateTimeOffset.UtcNow))
                .RequireOwnedByCallerAsync(InvoiceId, CancellationToken.None));

        Assert.Equal(InvoiceNotFoundException.PublicMessage, strangers.Message);
        Assert.Equal(strangers.Message, missing.Message);
        Assert.Equal(strangers.Message, noProfile.Message);
    }

    /// <summary>The rule must not refuse the caller their own invoice.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenInvoiceIsTheCallersOwn_AllowsAsync()
    {
        await AcceptingOwnership().RequireOwnedByCallerAsync(InvoiceId, CancellationToken.None);
    }

    /// <summary>Reading a stranger's invoice is refused before the shared read runs.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task GetMyInvoiceHandler_WhenInvoiceIsNotTheCallers_DoesNotDispatchTheSharedReadAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new GetMyInvoiceHandler(RefusingOwnership(), bus.Object);

        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => handler.HandleAsync(new GetMyInvoiceQuery(InvoiceId), CancellationToken.None));

        bus.Verify(
            b => b.InvokeAsync<InvoiceDto>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>
    /// Paying a stranger's invoice is refused before the shared write runs, so no card is charged
    /// against another customer's account.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task PayMyInvoiceHandler_WhenInvoiceIsNotTheCallers_DoesNotDispatchTheSharedWriteAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new PayMyInvoiceHandler(RefusingOwnership(), bus.Object);

        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => handler.HandleAsync(new PayMyInvoiceCommand(InvoiceId), CancellationToken.None));

        bus.Verify(
            b => b.InvokeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>On the caller's own invoice the payment goes through, currency and all.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task PayMyInvoiceHandler_WhenInvoiceIsTheCallersOwn_DispatchesTheSharedWriteAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new PayMyInvoiceHandler(AcceptingOwnership(), bus.Object);

        await handler.HandleAsync(new PayMyInvoiceCommand(InvoiceId, "AMD"), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync(
                It.Is<PayInvoiceCommand>(c => c.InvoiceId == InvoiceId && c.Currency == "AMD"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    /// <summary>Opening a gateway session on a stranger's invoice is refused before anything is registered.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task StartMyGatewayPaymentHandler_WhenInvoiceIsNotTheCallers_RefusesAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new StartMyGatewayPaymentHandler(RefusingOwnership(), bus.Object);

        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => handler.HandleAsync(
                new StartMyGatewayPaymentCommand(InvoiceId, "innovayse-inecobank", "https://example.com/return"),
                CancellationToken.None));

        bus.Verify(
            b => b.InvokeAsync<string>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>Probing a stranger's gateway session is refused before the gateway is contacted.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task CompleteMyGatewayPaymentHandler_WhenInvoiceIsNotTheCallers_RefusesAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new CompleteMyGatewayPaymentHandler(RefusingOwnership(), bus.Object);

        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => handler.HandleAsync(new CompleteMyGatewayPaymentCommand(InvoiceId), CancellationToken.None));

        bus.VerifyNoOtherCalls();
    }
}
