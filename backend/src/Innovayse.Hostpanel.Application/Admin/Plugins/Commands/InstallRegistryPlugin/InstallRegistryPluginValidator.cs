using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Hostpanel.Sdk.Packaging.Naming;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.InstallRegistryPlugin;

/// <summary>
///     Validates <see cref="InstallRegistryPluginCommand"/>. The id and the version are put into the registry address, so
///     both must have the shape the plugin id rule and semantic versioning allow before they reach the handler.
/// </summary>
public sealed class InstallRegistryPluginValidator : LocalizedValidator<InstallRegistryPluginCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public InstallRegistryPluginValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId).NotEmpty().WithMessage(L["PluginIdRequired"]);
        RuleFor(x => x.PluginId)
            .Must(PluginIdentifier.IsWellFormed)
            .When(x => !string.IsNullOrEmpty(x.PluginId))
            .WithMessage(L["PluginIdInvalid"]);

        RuleFor(x => x.Version).NotEmpty().WithMessage(L["PluginVersionRequired"]);
        RuleFor(x => x.Version)
            .Must(version => PluginVersion.TryParse(version, out _))
            .When(x => !string.IsNullOrEmpty(x.Version))
            .WithMessage(L["PluginVersionInvalid"]);
    }
}
