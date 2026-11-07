namespace Innovayse.Hostpanel.Domain.Support.Enums;

/// <summary>Represents the lifecycle status of a support ticket.</summary>
public enum TicketStatus
{
    /// <summary>Ticket is open and awaiting staff response.</summary>
    Open = 0,

    /// <summary>Staff replied, awaiting client response.</summary>
    AwaitingReply = 1,

    /// <summary>Ticket is closed.</summary>
    Closed = 3,
}
