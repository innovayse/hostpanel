using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Support;

/// <summary>Unit tests for <see cref="Ticket.HasReply"/>, the check a migration import uses to skip a reply it already wrote.</summary>
public sealed class TicketHasReplyTests
{
    /// <summary>The fixed moment every test acts at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The cutoff the checks use: the moment a hypothetical import job started.</summary>
    private static readonly DateTimeOffset Cutoff = Now;

    /// <summary>Creates an open ticket that already holds one staff reply from "Agent", written an hour before the cutoff.</summary>
    /// <returns>The ticket with its single reply.</returns>
    private static Ticket TicketWithOneReply()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);
        ticket.AddReply("On it", "Agent", isStaffReply: true, occurredAt: Now);
        ticket.Backdate(Now.AddHours(-2), TimeSpan.FromHours(1));
        return ticket;
    }

    /// <summary>A reply with the same message, author and staff flag is reported as held.</summary>
    [Fact]
    public void HasReply_ForTheSameReply_ReturnsTrue()
    {
        TicketWithOneReply().HasReply("On it", "Agent", isStaffReply: true, Cutoff).Should().BeTrue();
    }

    /// <summary>The comparison ignores case in the message and the author.</summary>
    [Fact]
    public void HasReply_IgnoresCase()
    {
        TicketWithOneReply().HasReply("ON IT", "agent", isStaffReply: true, Cutoff).Should().BeTrue();
    }

    /// <summary>A different message is a different reply.</summary>
    [Fact]
    public void HasReply_ForAnotherMessage_ReturnsFalse()
    {
        TicketWithOneReply().HasReply("Done", "Agent", isStaffReply: true, Cutoff).Should().BeFalse();
    }

    /// <summary>The same message from another author is a different reply.</summary>
    [Fact]
    public void HasReply_ForAnotherAuthor_ReturnsFalse()
    {
        TicketWithOneReply().HasReply("On it", "Someone else", isStaffReply: true, Cutoff).Should().BeFalse();
    }

    /// <summary>The same text posted by the client instead of staff is a different reply.</summary>
    [Fact]
    public void HasReply_ForAnotherStaffFlag_ReturnsFalse()
    {
        TicketWithOneReply().HasReply("On it", "Agent", isStaffReply: false, Cutoff).Should().BeFalse();
    }

    /// <summary>A ticket with no replies holds none.</summary>
    [Fact]
    public void HasReply_OnATicketWithoutReplies_ReturnsFalse()
    {
        var ticket = Ticket.Create(clientId: 1, subject: "Help", message: "Hi", departmentId: 1, priority: TicketPriority.Medium, occurredAt: Now);

        ticket.HasReply("On it", "Agent", isStaffReply: true, Cutoff).Should().BeFalse();
    }

    /// <summary>A reply written at or after the cutoff is not counted, so a repeat inside one run is not a duplicate.</summary>
    [Fact]
    public void HasReply_ForAReplyCreatedAfterTheCutoff_ReturnsFalse()
    {
        TicketWithOneReply().HasReply("On it", "Agent", isStaffReply: true, Now.AddHours(-3)).Should().BeFalse();
    }
}
