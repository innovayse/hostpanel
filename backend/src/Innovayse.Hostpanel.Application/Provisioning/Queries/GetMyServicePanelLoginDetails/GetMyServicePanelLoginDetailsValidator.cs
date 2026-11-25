using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetMyServicePanelLoginDetails;

/// <summary>Validates <see cref="GetMyServicePanelLoginDetailsQuery"/> before it reaches the handler.</summary>
public sealed class GetMyServicePanelLoginDetailsValidator : LocalizedValidator<GetMyServicePanelLoginDetailsQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyServicePanelLoginDetailsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
