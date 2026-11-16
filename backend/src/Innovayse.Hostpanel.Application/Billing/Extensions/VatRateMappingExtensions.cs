using Innovayse.Hostpanel.Application.Billing.VatRates.Common;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>Maps <see cref="VatRate"/> to <see cref="VatRateDto"/> in one place, so a field added to the DTO is added once.</summary>
public static class VatRateMappingExtensions
{
    /// <summary>Projects the query to <see cref="VatRateDto"/> rows.</summary>
    /// <param name="source">The rows to project.</param>
    /// <returns>The query projected to <see cref="VatRateDto"/>.</returns>
    public static IQueryable<VatRateDto> SelectDto(this IQueryable<VatRate> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(r => new VatRateDto(r.Id, r.Country, r.Kind, r.RatePercent, r.ValidFrom));
    }
}
