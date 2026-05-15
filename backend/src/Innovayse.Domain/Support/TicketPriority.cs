namespace Innovayse.Domain.Support;

/// <summary>Represents the priority level of a support ticket.</summary>
public enum TicketPriority
{
    /// <summary>Low-priority ticket — non-urgent issue.</summary>
    Low,

    /// <summary>Medium-priority ticket — standard issue.</summary>
    Medium,

    /// <summary>High-priority ticket — urgent or critical issue.</summary>
    High,
}
