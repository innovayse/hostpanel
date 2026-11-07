using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListRegistryPlugins;

/// <summary>
///     Validates <see cref="ListRegistryPluginsQuery"/>. The search text is forwarded to the registry, so its length
///     is bounded here and the category must be a defined one; <c>Page</c> and <c>PageSize</c> are clamped by the handler.
/// </summary>
public sealed class ListRegistryPluginsValidator : LocalizedValidator<ListRegistryPluginsQuery>
{
    /// <summary>The longest search text forwarded to the registry.</summary>
    private const int MaxSearchLength = 100;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListRegistryPluginsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Q).MaximumLength(MaxSearchLength).WithMessage(L["PluginRegistrySearchTooLong"]);
        RuleFor(x => x.Category).IsInEnum().WithMessage(L["PluginRegistryCategoryInvalid"]);
    }
}
