namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// A tag label attached to a support ticket.
/// Owned by <see cref="Ticket"/> — created via <see cref="Ticket.AddTag"/>.
/// </summary>
public sealed class TicketTag : Entity
{
    /// <summary>Gets the FK to the owning ticket.</summary>
    public int TicketId { get; private set; }

    /// <summary>Gets the tag name (lowercase, trimmed).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private TicketTag() : base(0) { }

    /// <summary>Creates a new tag for the given ticket.</summary>
    internal TicketTag(int ticketId, string name) : base(0)
    {
        TicketId = ticketId;
        Name = name;
    }
}
