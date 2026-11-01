using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallClientPlugin;

/// <summary>
/// Validates <see cref="CallClientPluginCommand"/>. It declares no rule: a plugin id or path that names nothing is
/// <c>PLUGIN_ROUTE_NOT_FOUND</c>, the same answer as an undeclared route, not a validation failure that would tell a
/// caller which ids are well formed.
/// </summary>
public sealed class CallClientPluginValidator : LocalizedValidator<CallClientPluginCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CallClientPluginValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
