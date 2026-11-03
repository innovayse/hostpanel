using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.PurgePluginData;

/// <summary>Validates <see cref="PurgePluginDataCommand"/> before it reaches the handler.</summary>
public sealed class PurgePluginDataValidator : LocalizedValidator<PurgePluginDataCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public PurgePluginDataValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(L["PluginIdRequired"]);

        // The id selects rows and names a settings key, so only the plugin id rule's shape reaches the handler. The shape, not the
        // full rule: a plugin installed under an id that was reserved later must still be purgeable after its removal.
        RuleFor(x => x.Id)
            .Must(PluginIdentifier.IsWellFormed)
            .When(x => !string.IsNullOrEmpty(x.Id))
            .WithMessage(L["PluginIdInvalid"]);
    }
}
