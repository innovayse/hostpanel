using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.SetClientExternalAccount;

/// <summary>Sets or clears a client's link to an account in another system.</summary>
public sealed class SetClientExternalAccountHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public SetClientExternalAccountHandler(IHostpanelDbContext context, IActivityLogger activity)
    {
        _context = context;
        _activity = activity;
    }

    /// <summary>
    /// Writes the link, or clears it when the command carries no provider and no account id.
    /// </summary>
    /// <param name="cmd">The set external account command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>CLIENT_NOT_FOUND</c> when the id names no client, or
    /// <c>EXTERNAL_ACCOUNT_ALREADY_LINKED</c> when another client holds the same provider and account
    /// id: including one that took it between the check and the save, which the unique index answers.
    /// </returns>
    public async Task<Result> HandleAsync(SetClientExternalAccountCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        string description;
        if (cmd.Provider is null || cmd.AccountId is null)
        {
            client.ClearExternalAccount();
            description = $"Cleared External Account - Client ID: {client.Id}";
        }
        else
        {
            client.SetExternalAccount(cmd.Provider, cmd.AccountId);

            // Compared on the trimmed values the entity stored, so the check sees what the index sees.
            if (await LinkedToAnotherAsync(client.ExternalAccountProvider!, client.ExternalAccountId!, client.Id, ct))
            {
                _context.DiscardPendingChanges();
                return Result.FailureCode(HostpanelErrorCodes.EXTERNAL_ACCOUNT_ALREADY_LINKED);
            }

            description = $"Set External Account - Client ID: {client.Id} - Provider: {client.ExternalAccountProvider}";
        }

        await _activity.RecordAsync(new ActivityLogEntry(client.Id, description), ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (client.ExternalAccountProvider is not null)
        {
            // Another writer took the pair between the check above and this save and the unique index
            // refused. Asked again rather than assumed: any other failed save must still surface.
            var provider = client.ExternalAccountProvider;
            var accountId = client.ExternalAccountId!;
            _context.DiscardPendingChanges();
            if (await LinkedToAnotherAsync(provider, accountId, cmd.ClientId, ct))
            {
                return Result.FailureCode(HostpanelErrorCodes.EXTERNAL_ACCOUNT_ALREADY_LINKED);
            }

            throw;
        }

        return Result.Success();
    }

    /// <summary>Whether a client other than the given one already holds the provider and account id.</summary>
    /// <param name="provider">The name of the other system.</param>
    /// <param name="accountId">The identifier the other system gives the client.</param>
    /// <param name="clientId">The client being linked, which does not count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when another client holds the pair.</returns>
    private Task<bool> LinkedToAnotherAsync(string provider, string accountId, int clientId, CancellationToken ct) =>
        _context.Clients.AsNoTracking().AnyAsync(
            c => c.Id != clientId && c.ExternalAccountProvider == provider && c.ExternalAccountId == accountId, ct);
}
