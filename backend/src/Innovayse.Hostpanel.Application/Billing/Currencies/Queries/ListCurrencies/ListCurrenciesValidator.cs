using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Queries.ListCurrencies;

/// <summary>
///     Validator for ListCurrenciesQuery.
///     Its one input is a flag, which every value of is valid, so no rule is declared: decided, not
///     forgotten.
/// </summary>
public sealed class ListCurrenciesValidator : LocalizedValidator<ListCurrenciesQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListCurrenciesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
