namespace Innovayse.Application.Support.Queries.GetTicket;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Loads a single ticket with all its replies and maps it to <see cref="TicketDto"/>.
/// </summary>
public sealed class GetTicketHandler(ITicketRepository repo, IDepartmentRepository departmentRepo)
{
    /// <summary>
    /// Handles <see cref="GetTicketQuery"/>.
    /// </summary>
    /// <param name="query">The get ticket query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="TicketDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the ticket is not found.</exception>
    public async Task<TicketDto> HandleAsync(GetTicketQuery query, CancellationToken ct)
    {
        var ticket = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"Ticket {query.Id} not found.");

        string? departmentName = null;
        if (ticket.DepartmentId.HasValue)
        {
            var dept = await departmentRepo.FindByIdAsync(ticket.DepartmentId.Value, ct);
            departmentName = dept?.Name;
        }

        var replies = ticket.Replies
            .Select(r => new TicketReplyDto(r.Id, r.Message, r.AuthorName, r.IsStaffReply, r.CreatedAt))
            .ToList();

        return new TicketDto(
            ticket.Id,
            ticket.ClientId,
            ticket.Subject,
            ticket.Message,
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.DepartmentId,
            departmentName,
            ticket.AssignedToStaffId,
            null,
            ticket.CreatedAt,
            ticket.IsFlagged,
            replies);
    }
}
