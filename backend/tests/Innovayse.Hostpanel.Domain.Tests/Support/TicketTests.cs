using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Domain.Support.Events;

namespace Innovayse.Hostpanel.Domain.Tests.Support;

/// <summary>Unit tests for the <see cref="Ticket"/> aggregate's status invariants.</summary>
public sealed class TicketTests
{
    /// <summary>The fixed moment every test acts at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Create, AddReply and Close date the events they raise with the moment the caller gives.</summary>
    [Fact]
    public void Events_CarryTheMomentTheCallerGives()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);
        ticket.AddReply("On it", "Agent", isStaffReply: true, occurredAt: Now.AddHours(1));
        ticket.Close(Now.AddHours(2));

        ticket.DomainEvents.OfType<TicketRepliedEvent>().Should().ContainSingle().Which.OccurredAt.Should().Be(Now.AddHours(1));
        ticket.DomainEvents.OfType<TicketClosedEvent>().Should().ContainSingle().Which.OccurredAt.Should().Be(Now.AddHours(2));
    }

    /// <summary>AddReply on a closed ticket throws <see cref="DomainRuleException"/> with <c>TICKET_REPLY_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void AddReply_WhenClosed_ThrowsWithTicketReplyNotAllowed()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);
        ticket.Close(Now);

        var act = () => ticket.AddReply("Still there?", "Agent", isStaffReply: true, occurredAt: Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(SupportErrors.TicketReplyNotAllowed);
    }

    /// <summary>Close on an already-closed ticket throws <see cref="DomainRuleException"/> with <c>TICKET_CLOSE_NOT_ALLOWED</c>.</summary>
    [Fact]
    public void Close_WhenAlreadyClosed_ThrowsWithTicketCloseNotAllowed()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);
        ticket.Close(Now);

        var act = () => ticket.Close(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(SupportErrors.TicketCloseNotAllowed);
    }

    /// <summary>Backdate puts the ticket in the past and spaces each reply one gap after the one before it.</summary>
    [Fact]
    public void Backdate_SpacesTheReplyTimestampsOneGapApart()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);
        ticket.AddReply("First", "Agent", isStaffReply: true, occurredAt: Now);
        ticket.AddReply("Second", "Customer", isStaffReply: false, occurredAt: Now);
        var openedAt = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        ticket.Backdate(openedAt, TimeSpan.FromHours(3));

        ticket.CreatedAt.Should().Be(openedAt);
        ticket.Replies.Select(r => r.CreatedAt).Should().Equal(openedAt.AddHours(3), openedAt.AddHours(6));
    }

    /// <summary>Backdate puts the feedback one gap after the last reply, so feedback never predates the answer it rates.</summary>
    [Fact]
    public void Backdate_PutsFeedbackAfterTheLastReply()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);
        ticket.AddReply("Fixed", "Agent", isStaffReply: true, occurredAt: Now);
        ticket.Close(Now);
        ticket.LeaveFeedback(9, null, "Customer", new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var openedAt = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

        ticket.Backdate(openedAt, TimeSpan.FromHours(2));

        ticket.FeedbackAt.Should().Be(openedAt.AddHours(4));
    }

    /// <summary>Backdate refuses a gap that is not positive, which would put replies before the ticket was opened.</summary>
    [Fact]
    public void Backdate_WhenGapIsNotPositive_Throws()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);

        var act = () => ticket.Backdate(Now, TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
