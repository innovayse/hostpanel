using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.LinkCardGatewayCustomer;

/// <summary>Handles <see cref="LinkCardGatewayCustomerCommand"/>.</summary>
public sealed class LinkCardGatewayCustomerHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public LinkCardGatewayCustomerHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Stores the customer id when the client has none, otherwise answers with the one already stored.</summary>
    /// <param name="cmd">The client and the customer to store.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The customer id the client ends up with (the given one, or the one a concurrent request stored
    /// first); <c>CLIENT_NOT_FOUND</c> when the id names no client; or
    /// <c>CARD_GATEWAY_CUSTOMER_ALREADY_LINKED</c> when the customer already belongs to another client.
    /// </returns>
    public async Task<Result<string>> HandleAsync(LinkCardGatewayCustomerCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        if (client.CardGatewayCustomerId is { } existing)
        {
            return Result<string>.Success(existing);
        }

        // The save is the conditional write: the customer id is a concurrency token, so the UPDATE only
        // happens while the row still holds the empty value read above, and no read-then-write gap exists.
        client.SetCardGatewayCustomerId(cmd.CustomerId);
        try
        {
            await _context.SaveChangesAsync(ct);
            return Result<string>.Success(cmd.CustomerId);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request stored its customer first; theirs stands.
            _context.DiscardPendingChanges();
        }
        catch (DbUpdateException)
        {
            // The unique index on the customer id: it names another client's customer.
            _context.DiscardPendingChanges();
            return Result<string>.FailureCode(HostpanelErrorCodes.CARD_GATEWAY_CUSTOMER_ALREADY_LINKED);
        }

        var stored = await _context.Clients
            .AsNoTracking()
            .Where(c => c.Id == cmd.ClientId)
            .Select(c => c.CardGatewayCustomerId)
            .FirstOrDefaultAsync(ct);
        return stored is null
            ? Result<string>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND)
            : Result<string>.Success(stored);
    }
}
