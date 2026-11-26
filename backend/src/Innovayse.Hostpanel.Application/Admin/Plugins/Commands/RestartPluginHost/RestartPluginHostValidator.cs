using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RestartPluginHost;

/// <summary>
/// Validates <see cref="RestartPluginHostCommand"/>. The command carries no input, so there is no rule to declare; the
/// validator exists so every use case has one, and a rule added later lands here.
/// </summary>
public sealed class RestartPluginHostValidator : LocalizedValidator<RestartPluginHostCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RestartPluginHostValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
