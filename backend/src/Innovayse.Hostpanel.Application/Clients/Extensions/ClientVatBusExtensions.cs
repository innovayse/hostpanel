using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// Asks for a client's VAT number to be checked after a write that changed it, in one place for every use case that writes
/// the number or the billing country, so none repeats the condition.
/// </summary>
public static class ClientVatBusExtensions
{
    /// <summary>
    /// Publishes <see cref="ClientVatCheckRequestedEvent"/> when the saved client holds a number no completed check judges
    /// yet and its country has a register to ask. Call it after the save, so the client has its id.
    /// </summary>
    /// <param name="bus">The Wolverine bus the request is published on.</param>
    /// <param name="client">The client that was just saved.</param>
    /// <param name="now">The current moment (UTC), read by the caller.</param>
    /// <returns>A task that completes when the request is published, or at once when there is nothing to check.</returns>
    public static async Task RequestVatCheckWhenPendingAsync(this IMessageBus bus, Client client, DateTimeOffset now)
    {
        if (client.VatCheckPending && VatRegisters.Covers(client.Country))
        {
            await bus.PublishAsync(new ClientVatCheckRequestedEvent(client.Id) { OccurredAt = now });
        }
    }
}
