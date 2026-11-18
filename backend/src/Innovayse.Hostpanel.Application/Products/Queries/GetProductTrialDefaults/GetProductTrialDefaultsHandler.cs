using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductTrialDefaults;

/// <summary>Answers the free-trial defaults and bounds from the domain, the one place they are stated.</summary>
public sealed class GetProductTrialDefaultsHandler
{
    /// <summary>Handles the query.</summary>
    /// <param name="qry">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The defaults and bounds. Never refuses.</returns>
    public Task<Result<ProductTrialDefaultsDto>> HandleAsync(GetProductTrialDefaultsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var defaults = ProductTrial.Default;
        var dto = new ProductTrialDefaultsDto(
            defaults.Days,
            defaults.AutoConvert,
            defaults.ReminderDays,
            defaults.GracePeriodDays,
            ProductTrial.MinDays,
            ProductTrial.MaxDays,
            ProductTrial.MinGracePeriodDays,
            ProductTrial.MaxGracePeriodDays,
            ProductTrial.MaxReminders);
        return Task.FromResult(Result<ProductTrialDefaultsDto>.Success(dto));
    }
}
