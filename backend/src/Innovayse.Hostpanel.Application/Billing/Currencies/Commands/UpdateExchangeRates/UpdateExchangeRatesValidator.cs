using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpdateExchangeRates;

/// <summary>
///     Validator for UpdateExchangeRatesCommand.
///     The command carries no input, so it declares no rules; the absence was decided, not forgotten.
///     Whether a base currency exists at all is the handler's answer, not a field rule.
/// </summary>
public sealed class UpdateExchangeRatesValidator : LocalizedValidator<UpdateExchangeRatesCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateExchangeRatesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
