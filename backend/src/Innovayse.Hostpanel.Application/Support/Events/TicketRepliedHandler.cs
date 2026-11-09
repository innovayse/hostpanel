using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Support.Events;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Support.Events;

/// <summary>
/// Handles <see cref="TicketRepliedEvent"/> raised when a reply is added to a ticket.
/// A staff reply notifies the client; a client reply notifies the assigned department.
/// </summary>
public sealed class TicketRepliedHandler
{
    /// <summary>Wolverine bus, used to dispatch the notification email.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus, used to dispatch the notification email.</param>
    /// <param name="context">The module's data context.</param>
    public TicketRepliedHandler(IMessageBus bus, IHostpanelDbContext context)
    {
        _bus = bus;
        _context = context;
    }

    /// <summary>
    /// Resolves the recipient (client or department, depending on the reply's author) and sends a
    /// ticket-replied notification email.
    /// </summary>
    /// <param name="evt">The ticket replied domain event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public async Task HandleAsync(TicketRepliedEvent evt, CancellationToken ct)
    {
        var ticket = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.Id == evt.TicketId)
            .Select(t => new { t.Subject, t.ClientId, t.DepartmentId })
            .FirstOrDefaultAsync(ct);
        if (ticket is null)
        {
            return;
        }

        var data = new TicketRepliedTemplateModel(new TicketTemplateData(evt.TicketId, ticket.Subject));

        if (evt.IsStaffReply)
        {
            var client = await _bus.FindClientBriefAsync(ticket.ClientId, withEmail: true, ct);
            if (client is not { Email: { } email })
            {
                return;
            }

            await _bus.InvokeAsync(
                new SendEmailCommand(
                    email,
                    client.Language,
                    "ticket-replied",
                    data,
                    new ContactCopy(ticket.ClientId, ContactNotification.Support),
                    IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "ticket-replied")),
                ct);
        }
        else if (ticket.DepartmentId is int departmentId)
        {
            var departmentEmail = await _context.Departments
                .AsNoTracking()
                .Where(d => d.Id == departmentId)
                .Select(d => d.Email)
                .FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(departmentEmail))
            {
                return;
            }

            await _bus.InvokeAsync(
                new SendEmailCommand(
                    departmentEmail,
                    null,
                    "ticket-replied",
                    data,
                    IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, "ticket-replied")),
                ct);
        }
    }
}
