using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetServerRevenueForecasts;

/// <summary>
/// Validator for <see cref="GetServerRevenueForecastsQuery"/>. The query carries no input, so it
/// declares no rules; the absence was decided, not forgotten.
/// </summary>
public sealed class GetServerRevenueForecastsValidator : LocalizedValidator<GetServerRevenueForecastsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetServerRevenueForecastsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
