using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.UpdateTaxRule;

/// <summary>Validates <see cref="UpdateTaxRuleCommand"/> before the handler executes.</summary>
public sealed class UpdateTaxRuleValidator : LocalizedValidator<UpdateTaxRuleCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateTaxRuleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.Name).MustBeTaxRuleName(L["TaxRuleNameInvalid"]);
        RuleFor(x => x.Country).MustBeTaxRuleCountry(L["TaxRuleCountryInvalid"]);
        RuleFor(x => x.Region).MustBeTaxRuleRegion(L["TaxRuleRegionInvalid"]);
        RuleFor(x => x.RatePercent).MustBeTaxRate(L["TaxRuleRateInvalid"], L["TaxRuleRateDecimalsInvalid"]);
    }
}
