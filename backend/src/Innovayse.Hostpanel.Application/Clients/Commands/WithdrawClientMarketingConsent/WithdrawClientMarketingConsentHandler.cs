using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.WithdrawClientMarketingConsent;

/// <summary>Loads the client, withdraws its marketing consent through the entity and saves. Repeating it changes nothing.</summary>
public sealed class WithdrawClientMarketingConsentHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public WithdrawClientMarketingConsentHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The client to withdraw consent for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public async Task<Result> HandleAsync(WithdrawClientMarketingConsentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        client.WithdrawMarketingConsent();

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
