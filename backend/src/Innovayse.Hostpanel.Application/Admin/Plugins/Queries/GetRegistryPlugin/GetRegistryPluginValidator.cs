using FluentValidation;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryPlugin;

/// <summary>
///     Validates <see cref="GetRegistryPluginQuery"/>. The id is put into the registry address, so it must have the
///     shape the plugin id rule allows before it reaches the handler.
/// </summary>
public sealed class GetRegistryPluginValidator : LocalizedValidator<GetRegistryPluginQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetRegistryPluginValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId).NotEmpty().WithMessage(L["PluginIdRequired"]);
        RuleFor(x => x.PluginId)
            .Must(PluginIdentifier.IsWellFormed)
            .When(x => !string.IsNullOrEmpty(x.PluginId))
            .WithMessage(L["PluginIdInvalid"]);
    }
}
