using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Commands.UpdateExchangeRatesCron;

/// <summary>Validates <see cref="UpdateExchangeRatesCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class UpdateExchangeRatesCronValidator : LocalizedValidator<UpdateExchangeRatesCronCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateExchangeRatesCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
