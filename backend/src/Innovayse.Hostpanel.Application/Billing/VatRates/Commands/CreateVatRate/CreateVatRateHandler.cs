using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.CreateVatRate;

/// <summary>Adds a VAT rate, refusing a second row for a country, kind and first day that has one.</summary>
public sealed class CreateVatRateHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public CreateVatRateHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The row's fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the new row's id, or <c>VAT_RATE_ALREADY_EXISTS</c> when the country, kind and day have a row.</returns>
    public async Task<Result<int>> HandleAsync(CreateVatRateCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var rate = VatRate.Create(cmd.Country, cmd.Kind, cmd.RatePercent, cmd.ValidFrom);
        if (await _context.VatRates.ForKey(rate.Country, rate.Kind, rate.ValidFrom).AnyAsync(ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.VAT_RATE_ALREADY_EXISTS);
        }

        _context.VatRates.Add(rate);
        await _context.SaveChangesAsync(ct);

        return Result.Success(rate.Id);
    }
}
