using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products;

namespace Innovayse.Hostpanel.Application.Products.Extensions;

/// <summary>Extension methods for mapping <see cref="ProductTrial"/> to its DTO and back.</summary>
public static class ProductTrialExtensions
{
    /// <summary>Maps free-trial settings to the DTO the API answers with.</summary>
    /// <param name="trial">The settings.</param>
    /// <returns>The DTO, its reminder days descending.</returns>
    public static ProductTrialDto ToDto(this ProductTrial trial)
    {
        ArgumentNullException.ThrowIfNull(trial);

        return new ProductTrialDto(trial.Enabled, trial.Days, trial.AutoConvert, trial.ReminderDays, trial.GracePeriodDays);
    }

    /// <summary>Maps free-trial settings the API received to the domain value.</summary>
    /// <param name="trial">The DTO.</param>
    /// <returns>The settings; validity is checked when they are applied to a product.</returns>
    public static ProductTrial ToDomain(this ProductTrialDto trial)
    {
        ArgumentNullException.ThrowIfNull(trial);

        return new ProductTrial(trial.Enabled, trial.Days, trial.AutoConvert, trial.ReminderDays, trial.GracePeriodDays);
    }
}
