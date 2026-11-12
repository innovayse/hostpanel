using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Orders.Events;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Notifications.Events;

/// <summary>
/// Handles <see cref="OrderRejectedFromReviewEvent"/> by mailing the client that staff refused their order, with the reason staff gave.
/// </summary>
public sealed class OrderRejectedFromReviewHandler
{
    /// <summary>The module's data context, to seed the template.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the client and sends the mail.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, to seed the template.</param>
    /// <param name="bus">Resolves the client and sends the mail.</param>
    public OrderRejectedFromReviewHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Mails the client that the order was refused, with the reason.</summary>
    /// <param name="evt">The refused order.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleAsync(OrderRejectedFromReviewEvent evt, CancellationToken ct)
    {
        var client = await _bus.FindClientBriefAsync(evt.ClientId, withEmail: true, ct);
        if (client is not { Email: { } email })
        {
            return;
        }

        await OrderRejectedTemplatesSeeder.EnsureSeededAsync(_context, ct);

        await _bus.InvokeAsync(
            new SendEmailCommand(
                email,
                client.Language,
                OrderRejectedTemplatesSeeder.RejectedSlug,
                new OrderRejectedTemplateModel(evt.OrderNumber, evt.Reason),
                new ContactCopy(evt.ClientId, ContactNotification.Product),
                IdempotencyKey: EmailIdempotencyKey.ForMessage(evt, OrderRejectedTemplatesSeeder.RejectedSlug)),
            ct);
    }
}
