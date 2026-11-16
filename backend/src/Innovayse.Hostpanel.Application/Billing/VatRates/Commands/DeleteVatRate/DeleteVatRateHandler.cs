using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.DeleteVatRate;

/// <summary>Deletes a VAT rate row. An invoice stores its own rate, so no invoice refers to the row.</summary>
public sealed class DeleteVatRateHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public DeleteVatRateHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The row to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>VAT_RATE_NOT_FOUND</c> when the id names no row.</returns>
    public async Task<Result> HandleAsync(DeleteVatRateCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var rate = await _context.VatRates.FirstOrDefaultAsync(r => r.Id == cmd.Id, ct);
        if (rate is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.VAT_RATE_NOT_FOUND);
        }

        _context.VatRates.Remove(rate);
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }
}
