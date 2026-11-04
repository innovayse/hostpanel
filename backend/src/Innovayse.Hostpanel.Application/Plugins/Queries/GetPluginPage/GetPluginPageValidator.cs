using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginPage;

/// <summary>
/// Validates <see cref="GetPluginPageQuery"/>. It declares no rule, by decision: a plugin id that is not
/// <c>PluginIdentifier.IsWellFormed</c>, a page id of the wrong shape or length, and an id that is well formed but names
/// nothing must all answer the same 404 <c>PLUGIN_PAGE_NOT_FOUND</c> as a disabled plugin. A rule here would answer a
/// 400 <c>VALIDATION_ERROR</c> instead and tell a caller which ids are well formed, so <see cref="GetPluginPageHandler"/>
/// checks the shape itself, before any lookup.
/// </summary>
public sealed class GetPluginPageValidator : LocalizedValidator<GetPluginPageQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPluginPageValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
