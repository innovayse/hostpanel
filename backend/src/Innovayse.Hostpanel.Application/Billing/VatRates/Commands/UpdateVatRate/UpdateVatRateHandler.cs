using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.UpdateVatRate;

/// <summary>Updates a VAT rate row, refusing a move onto a country, kind and first day another row holds.</summary>
public sealed class UpdateVatRateHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public UpdateVatRateHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The row to update and its new fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, <c>VAT_RATE_NOT_FOUND</c> when the id names no row, or <c>VAT_RATE_ALREADY_EXISTS</c> when another row holds the country, kind and day.</returns>
    public async Task<Result> HandleAsync(UpdateVatRateCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var rate = await _context.VatRates.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (rate is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.VAT_RATE_NOT_FOUND);
        }

        rate.Update(cmd.Country, cmd.Kind, cmd.RatePercent, cmd.ValidFrom);
        if (await _context.VatRates.AsNoTracking().ForKey(rate.Country, rate.Kind, rate.ValidFrom).AnyAsync(r => r.Id != rate.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.VAT_RATE_ALREADY_EXISTS);
        }

        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
