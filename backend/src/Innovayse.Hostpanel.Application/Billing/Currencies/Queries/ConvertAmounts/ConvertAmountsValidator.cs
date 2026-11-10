using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Queries.ConvertAmounts;

/// <summary>
/// Validates <see cref="ConvertAmountsQuery"/>. No rule is declared, decided and not forgotten: a code
/// without a stored currency is answered with the amount unconverted rather than refused, because
/// public price pages pass the visitor's choice straight through.
/// </summary>
public sealed class ConvertAmountsValidator : LocalizedValidator<ConvertAmountsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ConvertAmountsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
