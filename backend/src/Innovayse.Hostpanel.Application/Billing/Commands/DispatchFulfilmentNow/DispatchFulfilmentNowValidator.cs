using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;

/// <summary>Validates <see cref="DispatchFulfilmentNowCommand"/>: no rules; the command carries no input.</summary>
public sealed class DispatchFulfilmentNowValidator : LocalizedValidator<DispatchFulfilmentNowCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DispatchFulfilmentNowValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
