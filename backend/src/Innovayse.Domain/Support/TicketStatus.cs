namespace Innovayse.Domain.Support;

/// <summary>Represents the lifecycle status of a support ticket.</summary>
public enum TicketStatus
{
    /// <summary>Ticket is open and awaiting staff response.</summary>
    Open,

    /// <summary>Staff replied, awaiting client response.</summary>
    AwaitingReply,

    /// <summary>Ticket has been answered.</summary>
    Answered,

    /// <summary>Ticket is closed.</summary>
    Closed,
}
