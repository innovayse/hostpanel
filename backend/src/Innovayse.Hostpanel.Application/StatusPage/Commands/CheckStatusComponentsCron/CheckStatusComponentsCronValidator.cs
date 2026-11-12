using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CheckStatusComponentsCron;

/// <summary>Validates <see cref="CheckStatusComponentsCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class CheckStatusComponentsCronValidator : LocalizedValidator<CheckStatusComponentsCronCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CheckStatusComponentsCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
