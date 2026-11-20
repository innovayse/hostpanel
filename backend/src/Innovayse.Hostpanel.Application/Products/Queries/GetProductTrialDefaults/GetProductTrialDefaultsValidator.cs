using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductTrialDefaults;

/// <summary>
///     Validator for GetProductTrialDefaultsQuery.
///     The query has no input, so no rule is declared: decided, not forgotten.
/// </summary>
public sealed class GetProductTrialDefaultsValidator : LocalizedValidator<GetProductTrialDefaultsQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetProductTrialDefaultsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
