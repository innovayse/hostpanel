using FluentValidation;
using Hostpanel.Sdk.Packaging.Naming;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPluginSlotWidgets;

/// <summary>Validates <see cref="ListPluginSlotWidgetsQuery"/>: the plugin id must meet the plugin id rule.</summary>
public sealed class ListPluginSlotWidgetsValidator : LocalizedValidator<ListPluginSlotWidgetsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListPluginSlotWidgetsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId).NotEmpty().WithMessage(L["PluginIdRequired"]);
        RuleFor(x => x.PluginId)
            .Must(PluginIdentifier.IsValid)
            .When(x => !string.IsNullOrEmpty(x.PluginId))
            .WithMessage(L["PluginIdInvalid"]);
    }
}
