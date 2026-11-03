using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetClientPluginSlot;

/// <summary>
/// Validates <see cref="GetClientPluginSlotQuery"/>. It declares no rule: a slot id that names nothing is
/// <c>PLUGIN_SLOT_NOT_FOUND</c>, and a context id that is not the caller's is a 404, never a validation failure that
/// would tell a caller which ids are well formed or exist.
/// </summary>
public sealed class GetClientPluginSlotValidator : LocalizedValidator<GetClientPluginSlotQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetClientPluginSlotValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
