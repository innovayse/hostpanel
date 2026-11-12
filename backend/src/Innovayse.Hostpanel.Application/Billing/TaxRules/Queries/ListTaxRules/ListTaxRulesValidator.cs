using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.ListTaxRules;

/// <summary>Validates <see cref="ListTaxRulesQuery"/>; the query carries no input, so there is no rule.</summary>
public sealed class ListTaxRulesValidator : LocalizedValidator<ListTaxRulesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListTaxRulesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
