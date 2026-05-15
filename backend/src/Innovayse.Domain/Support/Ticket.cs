namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;
using Innovayse.Domain.Support.Events;

/// <summary>
/// Aggregate root representing a client support ticket.
/// Owns a collection of <see cref="TicketReply"/> entities.
/// Status transitions are driven by whether replies are from staff or client.
/// </summary>
public sealed class Ticket : AggregateRoot
{
    /// <summary>Internal mutable list of replies on this ticket.</summary>
    private readonly List<TicketReply> _replies = [];

    /// <summary>Gets the FK to the client who opened this ticket.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the subject line of this ticket.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the initial message body of this ticket.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Gets the current lifecycle status of this ticket.</summary>
    public TicketStatus Status { get; private set; }

    /// <summary>Gets the priority level assigned to this ticket.</summary>
    public TicketPriority Priority { get; private set; }

    /// <summary>Gets the FK to the department this ticket is assigned to, if any.</summary>
    public int? DepartmentId { get; private set; }

    /// <summary>Gets the FK to the staff member currently assigned to this ticket, if any.</summary>
    public int? AssignedToStaffId { get; private set; }

    /// <summary>Gets the UTC timestamp when this ticket was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets a read-only view of all replies on this ticket.</summary>
    public IReadOnlyList<TicketReply> Replies => _replies.AsReadOnly();

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Ticket() : base(0) { }

    /// <summary>
    /// Creates a new <see cref="Ticket"/> in <see cref="TicketStatus.Open"/> status
    /// and raises <see cref="TicketCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client opening this ticket.</param>
    /// <param name="subject">The ticket subject line.</param>
    /// <param name="message">The initial message body.</param>
    /// <param name="departmentId">FK to the target department.</param>
    /// <param name="priority">Priority level for this ticket.</param>
    /// <returns>A new <see cref="Ticket"/> in <see cref="TicketStatus.Open"/> state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="subject"/> or <paramref name="message"/> is null or whitespace.</exception>
    public static Ticket Create(int clientId, string subject, string message, int departmentId, TicketPriority priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var ticket = new Ticket
        {
            ClientId = clientId,
            Subject = subject,
            Message = message,
            DepartmentId = departmentId,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        ticket.AddDomainEvent(new TicketCreatedEvent(ticket.Id, clientId, departmentId));
        return ticket;
    }

    /// <summary>
    /// Adds a reply to this ticket and updates the status accordingly.
    /// Staff replies set status to <see cref="TicketStatus.AwaitingReply"/>;
    /// client replies set status to <see cref="TicketStatus.Open"/>.
    /// Raises <see cref="TicketRepliedEvent"/>.
    /// </summary>
    /// <param name="message">The reply body text.</param>
    /// <param name="authorName">Display name of the reply author.</param>
    /// <param name="isStaffReply">Whether the author is a staff member.</param>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is already closed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> or <paramref name="authorName"/> is null or whitespace.</exception>
    public void AddReply(string message, string authorName, bool isStaffReply)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Cannot add a reply to a closed ticket.");
        }

        var reply = TicketReply.Create(message, authorName, isStaffReply);
        _replies.Add(reply);

        Status = isStaffReply ? TicketStatus.AwaitingReply : TicketStatus.Open;
        AddDomainEvent(new TicketRepliedEvent(Id, isStaffReply));
    }

    /// <summary>
    /// Assigns this ticket to a staff member.
    /// </summary>
    /// <param name="staffId">FK to the staff member being assigned.</param>
    public void Assign(int staffId) => AssignedToStaffId = staffId;

    /// <summary>
    /// Closes this ticket and raises <see cref="TicketClosedEvent"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is already closed.</exception>
    public void Close()
    {
        if (Status == TicketStatus.Closed)
        {
            throw new InvalidOperationException("Ticket is already closed.");
        }

        Status = TicketStatus.Closed;
        AddDomainEvent(new TicketClosedEvent(Id, ClientId));
    }
}
