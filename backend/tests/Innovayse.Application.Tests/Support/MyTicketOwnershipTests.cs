namespace Innovayse.Application.Tests.Support;

using Innovayse.Application.Common;
using Innovayse.Application.Support.Commands.ReplyToMyTicket;
using Innovayse.Application.Support.Commands.ReplyToTicket;
using Innovayse.Application.Support.Common;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetMyTicket;
using Innovayse.Application.Support.Queries.GetTicket;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Moq;
using Wolverine;
using Xunit;

/// <summary>
/// Proves the client-facing ticket routes are scoped to the caller.
/// <para>
/// Before this, <c>MyTicketsController</c> dispatched <see cref="GetTicketQuery"/> and
/// <see cref="ReplyToTicketCommand"/> with nothing but the route id, so any authenticated client
/// could read and reply to any other customer's ticket by walking the sequential ids. These tests
/// assert both halves of the fix: that a stranger is refused, and that the refusal is
/// indistinguishable from "no such ticket" so the route cannot be used to enumerate ids.
/// </para>
/// </summary>
public sealed class MyTicketOwnershipTests
{
    /// <summary>Identity subject of the caller in every test below.</summary>
    private const string CallerSubject = "user-caller";

    /// <summary>A client id that is deliberately not the caller's, so ownership must not match.</summary>
    private const int StrangersClientId = 4242;

    /// <summary>The ticket id every probe asks for.</summary>
    private const int TicketId = 7;

    /// <summary>Builds the caller's own client record. Its <c>Id</c> is 0, the unsaved default.</summary>
    /// <returns>A client owned by <see cref="CallerSubject"/>.</returns>
    private static Client CallerClient() => Client.Create(CallerSubject, "Jane", "Doe", "jane@example.com");

    /// <summary>Builds a ticket belonging to the given client.</summary>
    /// <param name="clientId">FK to the owning client.</param>
    /// <returns>An open ticket.</returns>
    private static Ticket TicketOf(int clientId) =>
        Ticket.Create(clientId, "Help", "Card number is 4111...", departmentId: 5, TicketPriority.Medium);

    /// <summary>
    /// Builds a <see cref="TicketOwnership"/> over the given world.
    /// </summary>
    /// <param name="client">What the client repository answers for the caller's subject.</param>
    /// <param name="ticket">What the ticket repository answers for <see cref="TicketId"/>.</param>
    /// <returns>The rule under test.</returns>
    private static ITicketOwnership OwnershipOver(Client? client, Ticket? ticket)
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByUserIdAsync(CallerSubject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(client);

        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(r => r.FindByIdAsync(TicketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var caller = new Mock<ICurrentRequestContext>();
        caller.Setup(c => c.RequireUserId()).Returns(CallerSubject);

        return new TicketOwnership(tickets.Object, clients.Object, caller.Object);
    }

    /// <summary>An ownership rule that refuses everything, for the handler tests.</summary>
    /// <returns>A rule that throws <see cref="TicketNotFoundException"/>.</returns>
    private static ITicketOwnership RefusingOwnership() => OwnershipOver(CallerClient(), TicketOf(StrangersClientId));

    /// <summary>An ownership rule that accepts, for the handler tests.</summary>
    /// <returns>A rule that completes.</returns>
    private static ITicketOwnership AcceptingOwnership() => OwnershipOver(CallerClient(), TicketOf(clientId: 0));

    /// <summary>
    /// The whole point: a caller who does not own the ticket is refused rather than shown it.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenTicketBelongsToAnotherClient_RefusesAsync()
    {
        var ownership = OwnershipOver(CallerClient(), TicketOf(StrangersClientId));

        await Assert.ThrowsAsync<TicketNotFoundException>(
            () => ownership.RequireOwnedByCallerAsync(TicketId, CancellationToken.None));
    }

    /// <summary>
    /// A ticket that does not exist answers exactly as a ticket that is somebody else's -- same
    /// exception type, same sentence. Anything else would let the route be walked to find out
    /// which of the sequential ids are real.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenTicketDoesNotExist_AnswersAsForAStrangersTicketAsync()
    {
        var missing = await Assert.ThrowsAsync<TicketNotFoundException>(
            () => OwnershipOver(CallerClient(), ticket: null).RequireOwnedByCallerAsync(TicketId, CancellationToken.None));

        var strangers = await Assert.ThrowsAsync<TicketNotFoundException>(
            () => OwnershipOver(CallerClient(), TicketOf(StrangersClientId))
                .RequireOwnedByCallerAsync(TicketId, CancellationToken.None));

        Assert.Equal(strangers.Message, missing.Message);
        Assert.Equal(TicketNotFoundException.PublicMessage, missing.Message);
    }

    /// <summary>
    /// A caller with no client record is the third case that must not be distinguishable: an
    /// authenticated staff identity probing the client portal learns nothing about ticket ids.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenCallerHasNoClientRecord_RefusesAlikeAsync()
    {
        var ownership = OwnershipOver(client: null, TicketOf(StrangersClientId));

        var refusal = await Assert.ThrowsAsync<TicketNotFoundException>(
            () => ownership.RequireOwnedByCallerAsync(TicketId, CancellationToken.None));

        Assert.Equal(TicketNotFoundException.PublicMessage, refusal.Message);
    }

    /// <summary>The rule must not refuse the caller their own ticket.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task RequireOwnedByCallerAsync_WhenTicketIsTheCallersOwn_AllowsAsync()
    {
        var ownership = OwnershipOver(CallerClient(), TicketOf(clientId: 0));

        await ownership.RequireOwnedByCallerAsync(TicketId, CancellationToken.None);
    }

    /// <summary>
    /// Reading a stranger's ticket is refused before the shared read runs, so no ticket content
    /// is ever loaded on their behalf -- the refusal is not a filter applied to a result.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task GetMyTicketHandler_WhenTicketIsNotTheCallers_DoesNotDispatchTheSharedReadAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new GetMyTicketHandler(RefusingOwnership(), bus.Object);

        await Assert.ThrowsAsync<TicketNotFoundException>(
            () => handler.HandleAsync(new GetMyTicketQuery(TicketId), CancellationToken.None));

        bus.Verify(
            b => b.InvokeAsync<TicketDto>(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>
    /// Replying to a stranger's ticket is refused before the shared write runs, so nothing is
    /// appended to another customer's thread.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task ReplyToMyTicketHandler_WhenTicketIsNotTheCallers_DoesNotDispatchTheSharedWriteAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new ReplyToMyTicketHandler(RefusingOwnership(), bus.Object);

        await Assert.ThrowsAsync<TicketNotFoundException>(
            () => handler.HandleAsync(
                new ReplyToMyTicketCommand(TicketId, "let me in", "Mallory"), CancellationToken.None));

        bus.Verify(
            b => b.InvokeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>
    /// On the caller's own ticket the reply goes through, and always as a customer reply: a client
    /// must not be able to post a message the portal renders as coming from support.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task ReplyToMyTicketHandler_WhenTicketIsTheCallersOwn_RepliesAsTheCustomerAsync()
    {
        var bus = new Mock<IMessageBus>();
        var handler = new ReplyToMyTicketHandler(AcceptingOwnership(), bus.Object);

        await handler.HandleAsync(
            new ReplyToMyTicketCommand(TicketId, "any update?", "Jane"), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync(
                It.Is<ReplyToTicketCommand>(c => c.TicketId == TicketId && !c.IsStaffReply),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }
}
