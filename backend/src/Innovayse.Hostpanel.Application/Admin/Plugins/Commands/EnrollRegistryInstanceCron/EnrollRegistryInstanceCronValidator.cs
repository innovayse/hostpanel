using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.EnrollRegistryInstanceCron;

/// <summary>
///     Validates <see cref="EnrollRegistryInstanceCronCommand"/>. The command is a marker with no input, so there is no
///     rule to declare; the validator exists so every use case has one, and a rule added later lands here.
/// </summary>
public sealed class EnrollRegistryInstanceCronValidator : LocalizedValidator<EnrollRegistryInstanceCronCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public EnrollRegistryInstanceCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
