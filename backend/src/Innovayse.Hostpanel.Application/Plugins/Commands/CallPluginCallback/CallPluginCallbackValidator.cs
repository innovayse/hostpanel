using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Commands.CallPluginCallback;

/// <summary>
/// Validates <see cref="CallPluginCallbackCommand"/>. It declares no rule: a plugin id or path that names nothing is
/// <c>PLUGIN_ROUTE_NOT_FOUND</c>, the same answer as an undeclared route, so a caller learns nothing from a 400.
/// </summary>
public sealed class CallPluginCallbackValidator : LocalizedValidator<CallPluginCallbackCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CallPluginCallbackValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
