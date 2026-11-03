using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallAdminPlugin;

/// <summary>
/// Validates <see cref="CallAdminPluginCommand"/>. It declares no rule: a plugin id or path that names nothing is
/// <c>PLUGIN_ROUTE_NOT_FOUND</c>, the same answer as an undeclared route.
/// </summary>
public sealed class CallAdminPluginValidator : LocalizedValidator<CallAdminPluginCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CallAdminPluginValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
