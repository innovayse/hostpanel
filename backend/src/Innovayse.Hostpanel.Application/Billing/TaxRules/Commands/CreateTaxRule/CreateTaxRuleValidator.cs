using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.CreateTaxRule;

/// <summary>Validates <see cref="CreateTaxRuleCommand"/> before the handler executes.</summary>
public sealed class CreateTaxRuleValidator : LocalizedValidator<CreateTaxRuleCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateTaxRuleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Name).MustBeTaxRuleName(L["TaxRuleNameInvalid"]);
        RuleFor(x => x.Country).MustBeTaxRuleCountry(L["TaxRuleCountryInvalid"]);
        RuleFor(x => x.Region).MustBeTaxRuleRegion(L["TaxRuleRegionInvalid"]);
        RuleFor(x => x.RatePercent).MustBeTaxRate(L["TaxRuleRateInvalid"], L["TaxRuleRateDecimalsInvalid"]);
    }
}
