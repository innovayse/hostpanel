using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetAdminPluginSlot;

/// <summary>
/// Validates <see cref="GetAdminPluginSlotQuery"/>. It declares no rule: a slot id that names nothing is
/// <c>PLUGIN_SLOT_NOT_FOUND</c> and a context id that names no record is a 404, the same answers the client area gives.
/// </summary>
public sealed class GetAdminPluginSlotValidator : LocalizedValidator<GetAdminPluginSlotQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetAdminPluginSlotValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
