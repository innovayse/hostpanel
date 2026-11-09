using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetControlPanelSsoUrl;

/// <summary>Validates <see cref="GetControlPanelSsoUrlQuery"/> before the handler executes.</summary>
public sealed class GetControlPanelSsoUrlValidator : LocalizedValidator<GetControlPanelSsoUrlQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetControlPanelSsoUrlValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
