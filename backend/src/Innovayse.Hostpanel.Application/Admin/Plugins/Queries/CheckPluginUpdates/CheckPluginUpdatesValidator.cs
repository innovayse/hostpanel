using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.CheckPluginUpdates;

/// <summary>
///     Validates <see cref="CheckPluginUpdatesQuery"/>. The query carries no input, so there is no rule to
///     declare; the validator exists so every query has one, and a rule added later lands here.
/// </summary>
public sealed class CheckPluginUpdatesValidator : LocalizedValidator<CheckPluginUpdatesQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CheckPluginUpdatesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
