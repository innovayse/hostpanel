using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetPromotionsUsage;

/// <summary>
/// Validator for <see cref="GetPromotionsUsageQuery"/>. The query carries no input, so it
/// declares no rules; the absence was decided, not forgotten.
/// </summary>
public sealed class GetPromotionsUsageValidator : LocalizedValidator<GetPromotionsUsageQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPromotionsUsageValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
