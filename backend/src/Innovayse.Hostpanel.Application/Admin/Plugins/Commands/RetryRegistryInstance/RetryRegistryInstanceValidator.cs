using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RetryRegistryInstance;

/// <summary>
///     Validates <see cref="RetryRegistryInstanceCommand"/>. The command carries no input, so there is no rule to
///     declare; the validator exists so every use case has one, and a rule added later lands here.
/// </summary>
public sealed class RetryRegistryInstanceValidator : LocalizedValidator<RetryRegistryInstanceCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RetryRegistryInstanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
