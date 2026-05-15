namespace Innovayse.Application.Support.Events;

using Innovayse.Domain.Support.Events;
using Wolverine;

/// <summary>
/// Handles <see cref="TicketCreatedEvent"/> raised when a new support ticket is opened.
/// Currently a placeholder — will dispatch email notifications when the Notifications module is ready.
/// </summary>
public sealed class TicketCreatedHandler(IMessageBus bus)
{
    /// <summary>
    /// Processes the ticket created event.
    /// </summary>
    /// <param name="evt">The ticket created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A completed task (placeholder until notifications are implemented).</returns>
    public async Task HandleAsync(TicketCreatedEvent evt, CancellationToken ct)
    {
        // TODO: dispatch SendEmailCommand to department when notifications module is ready
        _ = bus;
        await Task.CompletedTask;
    }
}
