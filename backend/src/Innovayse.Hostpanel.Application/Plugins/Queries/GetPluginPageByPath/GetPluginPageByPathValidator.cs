using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPageByPath;

/// <summary>
/// Validates <see cref="GetPluginPageByPathQuery"/>. It declares no rule, by decision, for the reason
/// <c>GetPluginPageValidator</c> declares none: a plugin id that is not well formed, a missing path, a path of the wrong
/// shape or length, and a path that is well formed but names nothing must all answer the same 404
/// <c>PLUGIN_PAGE_NOT_FOUND</c> as a disabled plugin. A rule here would answer a 400 <c>VALIDATION_ERROR</c> instead and
/// tell a caller which paths are well formed, so <see cref="GetPluginPageByPathHandler"/> checks the shape itself,
/// before any lookup.
/// </summary>
public sealed class GetPluginPageByPathValidator : LocalizedValidator<GetPluginPageByPathQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPluginPageByPathValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
