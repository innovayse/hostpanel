using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Clients.Commands.CheckClientVat;

/// <summary>
/// Asks the VAT number service about a client's number and records the verdict on the client: valid with the registered
/// name and address, or invalid. When the service could not give a verdict nothing is recorded, so the client keeps the
/// status it had and a service that is down never turns a number invalid.
/// </summary>
public sealed class CheckClientVatHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The port to the service that holds the VAT number register.</summary>
    private readonly IVatNumberChecker _checker;

    /// <summary>The clock the date of the check is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="checker">The port to the service that holds the VAT number register.</param>
    /// <param name="clock">The clock the date of the check is read from.</param>
    public CheckClientVatHandler(IHostpanelDbContext context, IVatNumberChecker checker, IClock clock)
    {
        _context = context;
        _checker = checker;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when a verdict was recorded; <c>CLIENT_NOT_FOUND</c> when the id names no client; <c>CLIENT_VAT_NUMBER_MISSING</c>
    /// when the client holds no number; <c>CLIENT_VAT_COUNTRY_NOT_SUPPORTED</c> when the client's country has no register to ask;
    /// <c>VAT_CHECK_NOT_CONFIGURED</c> when the register of the client's country is not set up on this installation (the United
    /// Kingdom's without its credentials); or <c>VAT_SERVICE_UNAVAILABLE</c> when the service gave no verdict, in which case nothing was changed. A client
    /// changed by another writer during the call answers <c>CONFLICT</c> from the save.
    /// </returns>
    public async Task<Result> HandleAsync(CheckClientVatCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        if (client.TaxId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_VAT_NUMBER_MISSING);
        }

        if (!VatRegisters.Covers(client.Country))
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_VAT_COUNTRY_NOT_SUPPORTED);
        }

        var lookup = VatRegisters.LookupFor(client.Country!, client.TaxId);
        if (!_checker.IsConfiguredFor(lookup.Country))
        {
            return Result.FailureCode(HostpanelErrorCodes.VAT_CHECK_NOT_CONFIGURED);
        }

        // A prefix and nothing after it is not a number any register holds: no call is made for it.
        var answer = lookup.Number.Length == 0
            ? new VatNumberCheck(VatCheckOutcome.Invalid)
            : await _checker.CheckAsync(lookup, ct);
        if (answer.Outcome == VatCheckOutcome.Unavailable)
        {
            return Result.FailureCode(HostpanelErrorCodes.VAT_SERVICE_UNAVAILABLE);
        }

        client.RecordVatCheck(answer.Outcome, _clock.UtcNow, answer.Name, answer.Address);
        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
