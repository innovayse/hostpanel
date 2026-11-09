using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServiceControlPanelSsoUrl;

/// <summary>Validates <see cref="GetMyServiceControlPanelSsoUrlQuery"/> before it reaches the handler.</summary>
public sealed class GetMyServiceControlPanelSsoUrlValidator : LocalizedValidator<GetMyServiceControlPanelSsoUrlQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyServiceControlPanelSsoUrlValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
