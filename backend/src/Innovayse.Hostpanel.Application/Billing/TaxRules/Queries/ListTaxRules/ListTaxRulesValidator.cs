using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Queries.ListTaxRules;

/// <summary>
/// Validates <see cref="ListTaxRulesQuery"/>; the page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>) and the enabled filter is a plain switch with nothing more to check.
/// </summary>
public sealed class ListTaxRulesValidator : ListQueryValidator<ListTaxRulesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListTaxRulesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
