using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Support.Events;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Support.Events;

/// <summary>
/// Handles <see cref="TicketCreatedEvent"/> raised when a new support ticket is opened.
/// Notifies the department the ticket was routed to, if the department has an email set.
/// </summary>
public sealed class TicketCreatedHandler
{
    /// <summary>Wolverine bus, used to dispatch the notification email.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus, used to dispatch the notification email.</param>
    /// <param name="context">The module's data context.</param>
    public TicketCreatedHandler(IMessageBus bus, IHostpanelDbContext context)
    {
        _bus = bus;
        _context = context;
    }

    /// <summary>
    /// Resolves the ticket and its department, then sends a ticket-created notification email
    /// to the department if one is configured.
    /// </summary>
    /// <param name="evt">The ticket created domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(TicketCreatedEvent evt, CancellationToken ct)
    {
        var departmentEmail = await _context.Departments
            .AsNoTracking()
            .Where(d => d.Id == evt.DepartmentId)
            .Select(d => d.Email)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(departmentEmail))
        {
            return;
        }

        // The event arrives through a durable queue, so it can arrive after the demo reset truncated the
        // tables and reused the ids: a ticket that is gone or now belongs to another client is not the one
        // that was opened, and the department is not mailed about it.
        var subject = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.Id == evt.TicketId && t.ClientId == evt.ClientId)
            .Select(t => t.Subject)
            .FirstOrDefaultAsync(ct);
        if (subject is null)
        {
            return;
        }

        var data = new TicketCreatedTemplateModel(new TicketTemplateData(evt.TicketId, subject));

        await _bus.InvokeAsync(
            new SendEmailCommand(
                departmentEmail,
                null,
                "ticket-created",
                data,
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "ticket-created")),
            ct);
    }
}
