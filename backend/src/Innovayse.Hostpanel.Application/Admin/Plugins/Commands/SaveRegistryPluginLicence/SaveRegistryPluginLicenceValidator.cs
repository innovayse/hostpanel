using FluentValidation;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SaveRegistryPluginLicence;

/// <summary>
///     Validates <see cref="SaveRegistryPluginLicenceCommand"/>. The id is put into the stored setting key and the key is
///     sent in a request header, so both are held to a shape before they reach the handler.
/// </summary>
public sealed class SaveRegistryPluginLicenceValidator : LocalizedValidator<SaveRegistryPluginLicenceCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SaveRegistryPluginLicenceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId).NotEmpty().WithMessage(L["PluginIdRequired"]);
        RuleFor(x => x.PluginId)
            .Must(PluginIdentifier.IsWellFormed)
            .When(x => !string.IsNullOrEmpty(x.PluginId))
            .WithMessage(L["PluginIdInvalid"]);

        RuleFor(x => x.Key).NotEmpty().WithMessage(L["PluginLicenceKeyRequired"]);
        // The rule is shared with the registry client (RegistryLicenceKeyRule), so a key saved here is one it will send.
        RuleFor(x => x.Key)
            .Must(k => RegistryLicenceKeyRule.IsShortEnough(k.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.Key))
            .WithMessage(L["PluginLicenceKeyTooLong"]);
        RuleFor(x => x.Key)
            .Must(k => RegistryLicenceKeyRule.IsPrintableAscii(k.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.Key))
            .WithMessage(L["PluginLicenceKeyInvalid"]);
    }
}
