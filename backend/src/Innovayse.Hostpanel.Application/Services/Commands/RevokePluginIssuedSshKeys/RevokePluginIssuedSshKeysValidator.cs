using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.RevokePluginIssuedSshKeys;

/// <summary>Validates <see cref="RevokePluginIssuedSshKeysCommand"/>: the plugin id has the plugin id rule's shape.</summary>
public sealed class RevokePluginIssuedSshKeysValidator : LocalizedValidator<RevokePluginIssuedSshKeysCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RevokePluginIssuedSshKeysValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["PluginIdRequired"])
            .Must(PluginIdentifier.IsValid).WithMessage(L["PluginIdInvalid"]);
    }
}
