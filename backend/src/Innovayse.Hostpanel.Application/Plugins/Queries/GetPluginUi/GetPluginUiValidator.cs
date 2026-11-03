using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Plugins.Queries.GetPluginUi;

/// <summary>Validates <see cref="GetPluginUiQuery"/>. The area comes from the route and nothing else is asked, so there are no rules.</summary>
public sealed class GetPluginUiValidator : LocalizedValidator<GetPluginUiQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPluginUiValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
