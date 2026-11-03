using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>Validates <see cref="RemovePluginCommand"/> before it reaches the handler.</summary>
public sealed class RemovePluginValidator : LocalizedValidator<RemovePluginCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RemovePluginValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(L["PluginIdRequired"]);

        // The id names a folder on disk, so only the plugin id rule's shape reaches the handler. The shape, not the
        // full rule: a plugin installed under an id that was reserved later must still be removable.
        RuleFor(x => x.Id)
            .Must(PluginIdentifier.IsWellFormed)
            .When(x => !string.IsNullOrEmpty(x.Id))
            .WithMessage(L["PluginIdInvalid"]);
    }
}
