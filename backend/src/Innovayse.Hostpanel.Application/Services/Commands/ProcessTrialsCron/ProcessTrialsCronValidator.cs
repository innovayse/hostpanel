using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.ProcessTrialsCron;

/// <summary>Validates <see cref="ProcessTrialsCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class ProcessTrialsCronValidator : LocalizedValidator<ProcessTrialsCronCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ProcessTrialsCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
