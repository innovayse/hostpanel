using Innovayse.Hostpanel.Application.Clients.Commands.CheckClientVat;
using Innovayse.Hostpanel.Domain.Clients.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Events;

/// <summary>
/// Runs the VAT number check a saved change asked for, in the background. A check the service could not complete is only
/// logged: the client keeps the status it had, and the scheduled re-check tries the number again.
/// </summary>
public sealed class ClientVatCheckRequestedHandler
{
    /// <summary>The Wolverine bus the check command is invoked through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Records a check that was not completed.</summary>
    private readonly ILogger<ClientVatCheckRequestedHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">The Wolverine bus the check command is invoked through.</param>
    /// <param name="logger">Records a check that was not completed.</param>
    public ClientVatCheckRequestedHandler(IMessageBus bus, ILogger<ClientVatCheckRequestedHandler> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Checks the client's number.</summary>
    /// <param name="evt">The request published after the save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the check was made or refused.</returns>
    public async Task HandleAsync(ClientVatCheckRequestedEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var outcome = await _bus.InvokeAsync<Result>(new CheckClientVatCommand(evt.ClientId), ct);
        if (outcome.IsFailure)
        {
            _logger.LogInformation(
                "The VAT number check of client {ClientId} was not completed ({ErrorCode}); its status is unchanged.",
                evt.ClientId, outcome.ErrorCode);
        }
    }
}
