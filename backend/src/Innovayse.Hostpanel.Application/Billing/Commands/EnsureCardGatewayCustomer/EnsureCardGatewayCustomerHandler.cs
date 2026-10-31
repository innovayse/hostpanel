using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Commands.LinkCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Clients.Queries.GetCardGatewayCustomer;
using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.EnsureCardGatewayCustomer;

/// <summary>
/// Handles <see cref="EnsureCardGatewayCustomerCommand"/>: one card gateway customer per client, however
/// many requests ask for it at the same moment.
/// </summary>
/// <remarks>
/// Four layers, each covering what the one before cannot. The gateway call carries an idempotency key per
/// client, so parallel creations answer with one customer. The customer carries the client id, so one
/// created but never stored (a crash between the two steps) is found and adopted instead of duplicated.
/// Storing it is one conditional write in the Clients module, so exactly one request wins. And a unique
/// index on the stored id stops a customer ever belonging to two clients.
/// </remarks>
public sealed class EnsureCardGatewayCustomerHandler
{
    /// <summary>Card payment gateway, for opening or finding the customer.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Message bus, for the Clients module's customer reads and its conditional write.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<EnsureCardGatewayCustomerHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="cardGateway">Card payment gateway, for opening or finding the customer.</param>
    /// <param name="bus">Message bus, for the Clients module's customer reads and its conditional write.</param>
    /// <param name="logger">Structured logger.</param>
    public EnsureCardGatewayCustomerHandler(ICardPaymentGateway cardGateway, IMessageBus bus, ILogger<EnsureCardGatewayCustomerHandler> logger)
    {
        _cardGateway = cardGateway;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Returns the client's card gateway customer, opening one when the client has none.</summary>
    /// <param name="cmd">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The customer id; <c>PAYMENT_METHOD_UNAVAILABLE</c> when the deployment has no card gateway;
    /// <c>CLIENT_NOT_FOUND</c> when the id names no client; or the gateway's refusal as
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> / <c>PAYMENT_METHOD_UPSTREAM_FAILED</c>.
    /// </returns>
    public async Task<Result<string>> HandleAsync(EnsureCardGatewayCustomerCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var configured = _cardGateway.RequireConfigured();
        if (configured.IsFailure)
        {
            return Result<string>.FailureCode(configured.ErrorCode!);
        }

        var stored = await ReadStoredAsync(cmd.ClientId, ct);
        if (stored.IsFailure || stored.Value!.CustomerId is not null)
        {
            return stored.IsFailure
                ? Result<string>.FailureCode(stored.ErrorCode!)
                : Result<string>.Success(stored.Value!.CustomerId!);
        }

        var brief = await _bus.GetRequiredClientBriefAsync(cmd.ClientId, withEmail: true, ct);

        // A customer opened for this client but never stored (a crash between the two steps) is adopted.
        // A failed search is not fatal: the creation below carries the idempotency key.
        var found = await _cardGateway.FindCustomerAsync(new CardFindCustomerInstruction(cmd.ClientId), ct);
        var candidate = found.CustomerId;
        var openedHere = false;
        if (candidate is null)
        {
            var created = await _cardGateway.CreateCustomerAsync(
                new CardCreateCustomerInstruction(
                    cmd.ClientId,
                    brief.FullName,
                    brief.Email ?? string.Empty,
                    string.Create(CultureInfo.InvariantCulture, $"customer-create-client-{cmd.ClientId}")),
                ct);
            if (created.Failure is { } failure)
            {
                // A request racing this one may have stored its customer while the gateway was failing here.
                var raced = await ReadStoredAsync(cmd.ClientId, ct);
                return raced.IsSuccess && raced.Value!.CustomerId is { } winner
                    ? Result<string>.Success(winner)
                    : Result<string>.FailureCode(failure.ToPaymentMethodErrorCode());
            }

            candidate = created.CustomerId!;
            openedHere = true;
        }

        var linked = await _bus.InvokeAsync<Result<string>>(new LinkCardGatewayCustomerCommand(cmd.ClientId, candidate), ct);
        if (linked.IsFailure)
        {
            return linked;
        }

        if (openedHere && linked.Value != candidate)
        {
            // Another request stored a different customer first: ours is surplus. Dropping it is housekeeping, so a failure is only logged.
            var deleted = await _cardGateway.DeleteCustomerAsync(new CardCustomerInstruction(candidate), ct);
            if (deleted.Failure is not null)
            {
                _logger.LogWarning("Could not delete the surplus card gateway customer {CustomerId}: {Failure}", candidate, deleted.Failure);
            }
        }

        return linked;
    }

    /// <summary>Reads the customer id stored for a client.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored link, or <c>CLIENT_NOT_FOUND</c>.</returns>
    private Task<Result<CardGatewayCustomerDto>> ReadStoredAsync(int clientId, CancellationToken ct) =>
        _bus.InvokeAsync<Result<CardGatewayCustomerDto>>(new GetCardGatewayCustomerQuery(clientId), ct);
}
