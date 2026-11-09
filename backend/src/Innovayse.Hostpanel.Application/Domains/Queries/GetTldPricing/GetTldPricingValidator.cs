using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetTldPricing;

/// <summary>
/// Validates <see cref="GetTldPricingQuery"/> inputs before the handler executes. There are no
/// rules: <see cref="GetTldPricingQuery.TargetCurrency"/> is optional and any unrecognized code
/// is answered in unconverted prices, so no input value refuses the request.
/// </summary>
public sealed class GetTldPricingValidator : LocalizedValidator<GetTldPricingQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetTldPricingValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
