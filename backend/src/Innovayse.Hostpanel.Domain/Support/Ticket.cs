using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Domain.Support.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Support;

/// <summary>
/// Aggregate root representing a client support ticket.
/// Owns a collection of <see cref="TicketReply"/> entities.
/// Status transitions are driven by whether replies are from staff or client.
/// </summary>
public sealed class Ticket : BaseEntity, IAggregateRoot
{
    /// <summary>Internal mutable list of replies on this ticket.</summary>
    private readonly List<TicketReply> _replies = [];

    /// <summary>Internal mutable list of tags on this ticket.</summary>
    private readonly List<TicketTag> _tags = [];

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

    /// <summary>Gets whether this ticket has been flagged by staff for attention.</summary>
    public bool IsFlagged { get; private set; }

    /// <summary>Gets the client feedback rating (1 to 10), or null if not yet rated.</summary>
    public int? Rating { get; private set; }

    /// <summary>Gets the optional feedback comment left by the client.</summary>
    public string? FeedbackComment { get; private set; }

    /// <summary>Gets the name of the client who left the feedback.</summary>
    public string? FeedbackLeftBy { get; private set; }

    /// <summary>Gets the UTC timestamp when feedback was submitted, or null if not yet rated.</summary>
    public DateTimeOffset? FeedbackAt { get; private set; }

    /// <summary>Gets a read-only view of all replies on this ticket.</summary>
    public IReadOnlyList<TicketReply> Replies => _replies.AsReadOnly();

    /// <summary>Gets a read-only view of all tags on this ticket.</summary>
    public IReadOnlyList<TicketTag> Tags => _tags.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private Ticket() { }

    /// <summary>
    /// Creates a new <see cref="Ticket"/> in <see cref="TicketStatus.Open"/> status
    /// and raises <see cref="TicketCreatedEvent"/>.
    /// </summary>
    /// <param name="clientId">FK to the client opening this ticket.</param>
    /// <param name="subject">The ticket subject line.</param>
    /// <param name="message">The initial message body.</param>
    /// <param name="departmentId">FK to the target department.</param>
    /// <param name="priority">Priority level for this ticket.</param>
    /// <param name="occurredAt">The moment the ticket was opened, UTC.</param>
    /// <returns>A new <see cref="Ticket"/> in <see cref="TicketStatus.Open"/> state.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="subject"/> or <paramref name="message"/> is null or whitespace.</exception>
    public static Ticket Create(int clientId, string subject, string message, int departmentId, TicketPriority priority, DateTimeOffset occurredAt)
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
        };

        ticket.AddDomainEvent(() => new TicketCreatedEvent(ticket.Id, clientId, departmentId) { OccurredAt = occurredAt });
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
    /// <param name="occurredAt">The moment the reply was posted, UTC.</param>
    /// <exception cref="DomainRuleException">Thrown when the ticket is already closed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> or <paramref name="authorName"/> is null or whitespace.</exception>
    public void AddReply(string message, string authorName, bool isStaffReply, DateTimeOffset occurredAt)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new DomainRuleException(SupportErrors.TicketReplyNotAllowed, "Cannot add a reply to a closed ticket.");
        }

        var reply = TicketReply.Create(message, authorName, isStaffReply);
        _replies.Add(reply);

        Status = isStaffReply ? TicketStatus.AwaitingReply : TicketStatus.Open;
        AddDomainEvent(new TicketRepliedEvent(Id, isStaffReply) { OccurredAt = occurredAt });
    }

    /// <summary>
    /// Tells whether this ticket already held, before <paramref name="createdBefore"/>, a reply with the same
    /// message, author and staff flag (message and author compared case-insensitively), so an import can skip
    /// a reply an earlier run wrote. Replies created at or after the cutoff are ignored: a source reply carries
    /// no date or id, so the same person legitimately writing the same words twice in one run must not be
    /// mistaken for a duplicate of the reply the run itself just added.
    /// </summary>
    /// <param name="message">The reply body text.</param>
    /// <param name="authorName">Display name of the reply author.</param>
    /// <param name="isStaffReply">Whether the author is a staff member.</param>
    /// <param name="createdBefore">The cutoff, UTC: only replies created strictly before it count.</param>
    /// <returns><see langword="true"/> when an identical reply older than the cutoff is on the ticket.</returns>
    public bool HasReply(string message, string authorName, bool isStaffReply, DateTimeOffset createdBefore) =>
        _replies.Any(r =>
            r.CreatedAt < createdBefore
            && r.IsStaffReply == isStaffReply
            && string.Equals(r.Message, message, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.AuthorName, authorName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Moves the whole history of the ticket into the past, for tickets seeded from history rather than opened now:
    /// the ticket is opened at <paramref name="openedAt"/>, each reply follows the one before it by
    /// <paramref name="gap"/>, and feedback, when it was left, comes one gap after the last reply.
    /// </summary>
    /// <param name="openedAt">The moment the ticket was really opened (UTC).</param>
    /// <param name="gap">The time between one message and the next; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gap"/> is zero or negative.</exception>
    public void Backdate(DateTimeOffset openedAt, TimeSpan gap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(gap, TimeSpan.Zero);

        CreatedAt = openedAt;
        var at = openedAt;
        foreach (var reply in _replies)
        {
            at += gap;
            reply.Backdate(at);
        }

        if (FeedbackAt is not null)
        {
            FeedbackAt = at + gap;
        }
    }

    /// <summary>
    /// Assigns this ticket to a staff member.
    /// </summary>
    /// <param name="staffId">FK to the staff member being assigned.</param>
    public void Assign(int staffId) => AssignedToStaffId = staffId;

    /// <summary>
    /// Closes this ticket and raises <see cref="TicketClosedEvent"/>.
    /// </summary>
    /// <param name="occurredAt">The moment the ticket was closed, UTC.</param>
    /// <exception cref="DomainRuleException">Thrown when the ticket is already closed.</exception>
    public void Close(DateTimeOffset occurredAt)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new DomainRuleException(SupportErrors.TicketCloseNotAllowed, "Ticket is already closed.");
        }

        Status = TicketStatus.Closed;
        AddDomainEvent(new TicketClosedEvent(Id, ClientId) { OccurredAt = occurredAt });
    }

    /// <summary>
    /// Changes the priority level of this ticket.
    /// </summary>
    /// <param name="priority">The new priority level.</param>
    public void ChangePriority(TicketPriority priority) => Priority = priority;

    /// <summary>
    /// Changes the department assignment of this ticket.
    /// </summary>
    /// <param name="departmentId">FK to the new department, or <see langword="null"/> to unassign.</param>
    public void ChangeDepartment(int? departmentId) => DepartmentId = departmentId;

    /// <summary>
    /// Toggles the flagged state of this ticket.
    /// </summary>
    public void ToggleFlag() => IsFlagged = !IsFlagged;

    /// <summary>
    /// Reopens a closed ticket by setting status back to <see cref="TicketStatus.Open"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not currently closed.</exception>
    public void Reopen()
    {
        if (Status != TicketStatus.Closed)
        {
            throw new InvalidOperationException("Only closed tickets can be reopened.");
        }

        Status = TicketStatus.Open;
    }

    /// <summary>
    /// Records client feedback for this ticket.
    /// </summary>
    /// <param name="rating">Score from 1 to 10.</param>
    /// <param name="comment">Optional feedback comment.</param>
    /// <param name="leftBy">Display name of the client submitting the feedback.</param>
    /// <param name="leftAt">When the feedback was left, UTC.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when rating is outside 1 to 10.</exception>
    public void LeaveFeedback(int rating, string? comment, string leftBy, DateTimeOffset leftAt)
    {
        if (rating < 1 || rating > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 10.");
        }

        Rating = rating;
        FeedbackComment = comment;
        FeedbackLeftBy = leftBy;
        FeedbackAt = leftAt;
    }

    /// <summary>Adds a tag to this ticket. Duplicate tags are ignored.</summary>
    /// <param name="tag">Tag text.</param>
    public void AddTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        tag = tag.Trim().ToLowerInvariant();
        if (_tags.Any(t => t.Name == tag))
        {
            return;
        }

        _tags.Add(new TicketTag(Id, tag));
    }

    /// <summary>Removes a tag from this ticket.</summary>
    /// <param name="tag">Tag text to remove.</param>
    public void RemoveTag(string tag)
    {
        tag = tag.Trim().ToLowerInvariant();
        var existing = _tags.FirstOrDefault(t => t.Name == tag);
        if (existing is not null)
        {
            _tags.Remove(existing);
        }
    }
}
