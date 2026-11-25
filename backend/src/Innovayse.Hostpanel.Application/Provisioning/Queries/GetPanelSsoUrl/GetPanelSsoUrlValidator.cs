using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelSsoUrl;

/// <summary>Validates <see cref="GetPanelSsoUrlQuery"/> before the handler executes.</summary>
public sealed class GetPanelSsoUrlValidator : LocalizedValidator<GetPanelSsoUrlQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPanelSsoUrlValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
