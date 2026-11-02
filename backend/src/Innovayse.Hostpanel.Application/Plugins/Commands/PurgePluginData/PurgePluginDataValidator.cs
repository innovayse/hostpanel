using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.PurgePluginData;

/// <summary>Validates <see cref="PurgePluginDataCommand"/> before it reaches the handler.</summary>
public sealed class PurgePluginDataValidator : LocalizedValidator<PurgePluginDataCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public PurgePluginDataValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId).NotEmpty().WithMessage(L["PluginIdRequired"]);

        // Only a well-formed plugin id may select rows, the same shape the removal route accepts.
        RuleFor(x => x.PluginId)
            .Must(PluginIdentifier.IsValid)
            .When(x => !string.IsNullOrEmpty(x.PluginId))
            .WithMessage(L["PluginIdInvalid"]);
    }
}
