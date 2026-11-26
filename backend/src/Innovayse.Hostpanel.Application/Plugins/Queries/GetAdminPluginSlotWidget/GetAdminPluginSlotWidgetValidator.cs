using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlotWidget;

/// <summary>
/// Validates <see cref="GetAdminPluginSlotWidgetQuery"/>. It declares no rule: an id that names nothing is a 404, the
/// same answers the admin slot index gives.
/// </summary>
public sealed class GetAdminPluginSlotWidgetValidator : LocalizedValidator<GetAdminPluginSlotWidgetQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetAdminPluginSlotWidgetValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
