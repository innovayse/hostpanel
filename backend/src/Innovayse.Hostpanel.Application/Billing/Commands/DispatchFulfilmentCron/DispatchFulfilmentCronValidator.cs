using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentCron;

/// <summary>Validates <see cref="DispatchFulfilmentCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class DispatchFulfilmentCronValidator : LocalizedValidator<DispatchFulfilmentCronCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DispatchFulfilmentCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
