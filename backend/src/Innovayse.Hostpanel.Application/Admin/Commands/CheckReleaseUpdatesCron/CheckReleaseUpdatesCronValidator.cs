using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Commands.CheckReleaseUpdatesCron;

/// <summary>Validates <see cref="CheckReleaseUpdatesCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class CheckReleaseUpdatesCronValidator : LocalizedValidator<CheckReleaseUpdatesCronCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CheckReleaseUpdatesCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
