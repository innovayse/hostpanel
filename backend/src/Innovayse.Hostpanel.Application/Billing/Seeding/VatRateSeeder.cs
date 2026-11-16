using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Seeding;

/// <summary>
/// Imports the VAT rates that ship with the application (<see cref="VatRateCatalogue"/>) at startup, on every environment.
/// A shipped rate is added only when the country holds no standard row that starts on or after the shipped rate's first day,
/// and a row that exists is never touched. So a rate staff edited on the VAT rates screen survives every restart, including a
/// shipped row whose first day was moved later (it is not brought back beside the edited row, which would leave two live
/// rates), and a new release that carries a changed rate brings it as a new row. A shipped row that was deleted comes back
/// only while nothing newer is held for its country.
/// </summary>
public static class VatRateSeeder
{
    /// <summary>Adds the shipped standard rates that no row of the table already stands for.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of rows added.</returns>
    public static async Task<int> EnsureSeededAsync(IHostpanelDbContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var held = await context.VatRates.AsNoTracking()
            .Where(r => r.Kind == VatRateKind.Standard)
            .GroupBy(r => r.Country)
            .Select(g => new { Country = g.Key, Latest = g.Max(r => r.ValidFrom) })
            .ToListAsync(ct);
        var latestHeld = held.ToDictionary(r => r.Country, r => r.Latest);

        // A shipped rate is superseded by a held row that starts on the same day or later: either it is that very row, or
        // staff moved it or entered a newer rate, and adding the shipped one would put two rates in force for the country.
        var missing = VatRateCatalogue.Rates
            .Where(r => !latestHeld.TryGetValue(r.Country, out var latest) || latest < r.ValidFrom)
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
