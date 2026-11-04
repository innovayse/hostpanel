using FluentValidation;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

/// <summary>
/// Validates <see cref="SaveIntegrationConfigCommand"/> before it reaches the handler. The shape of
/// the body is checked here; which fields the integration declares, and whether each value fits its
/// type, is decided by the handler, which knows the integration (a slug that names none answers
/// <c>INTEGRATION_NOT_FOUND</c>).
/// </summary>
public sealed class SaveIntegrationConfigValidator : LocalizedValidator<SaveIntegrationConfigCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SaveIntegrationConfigValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Slug).NotEmpty().WithMessage(L["IntegrationSlugRequired"]);
        RuleFor(x => x.Config).NotNull().WithMessage(L["IntegrationConfigRequired"]);
        RuleFor(x => x.Config)
            .Must(config => config.Values.All(v => v is null || v.Length <= ProtectedValueLimits.SettingValue))
            .WithMessage(L["SettingValueTooLong", ProtectedValueLimits.SettingValue])
            .When(x => x.Config is not null);
    }
}
