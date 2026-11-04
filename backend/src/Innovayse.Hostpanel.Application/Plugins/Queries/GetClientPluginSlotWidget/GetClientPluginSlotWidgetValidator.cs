using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlotWidget;

/// <summary>
/// Validates <see cref="GetClientPluginSlotWidgetQuery"/>. It declares no rule, as the slot index does not: an id that
/// names nothing is a 404, never a validation failure that would tell a caller which ids are well formed or exist.
/// </summary>
public sealed class GetClientPluginSlotWidgetValidator : LocalizedValidator<GetClientPluginSlotWidgetQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetClientPluginSlotWidgetValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
