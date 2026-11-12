using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.GetTaxRule;

/// <summary>Validates <see cref="GetTaxRuleQuery"/> before it reaches the handler.</summary>
public sealed class GetTaxRuleValidator : LocalizedValidator<GetTaxRuleQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetTaxRuleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
