using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Support;

/// <summary>
/// A tag label attached to a support ticket.
/// Owned by <see cref="Ticket"/>: created via <see cref="Ticket.AddTag"/>.
/// </summary>
public sealed class TicketTag : BaseEntity
{
    /// <summary>Gets the FK to the owning ticket.</summary>
    public int TicketId { get; private set; }

    /// <summary>Gets the tag name (lowercase, trimmed).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private TicketTag() { }

    /// <summary>Creates a new tag for the given ticket.</summary>
    /// <param name="ticketId">The ticket identifier.</param>
    /// <param name="name">The tag text.</param>
    internal TicketTag(int ticketId, string name)
    {
        TicketId = ticketId;
        Name = name;
    }
}
