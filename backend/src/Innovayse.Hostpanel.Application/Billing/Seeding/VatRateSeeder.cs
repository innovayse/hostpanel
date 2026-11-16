using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Seeding;

/// <summary>
/// Imports the VAT rates that ship with the application (<see cref="VatRateCatalogue"/>) at startup, on every environment.
/// It adds the rows an installation lacks and never touches one that exists, so a rate staff edited on the VAT rates screen
/// survives every restart, and a new release that carries a changed rate brings it as a new row.
/// </summary>
public static class VatRateSeeder
{
    /// <summary>Adds the shipped standard rates the table does not hold yet.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of rows added.</returns>
    public static async Task<int> EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var held = await context.VatRates.AsNoTracking()
            .Where(r => r.Kind == VatRateKind.Standard)
            .Select(r => new { r.Country, r.ValidFrom })
            .ToListAsync(ct);
        var heldKeys = held.Select(r => (r.Country, r.ValidFrom)).ToHashSet();

        var missing = VatRateCatalogue.Rates
            .Where(r => !heldKeys.Contains((r.Country, r.ValidFrom)))
            .Select(r => VatRate.Create(r.Country, VatRateKind.Standard, r.RatePercent, r.ValidFrom))
            .ToList();
        if (missing.Count == 0)
        {
            return 0;
        }

        context.VatRates.AddRange(missing);
        await context.SaveChangesAsync(ct);

        return missing.Count;
    }
}
