using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelLoginDetails;

/// <summary>Validates <see cref="GetPanelLoginDetailsQuery"/> before the handler executes.</summary>
public sealed class GetPanelLoginDetailsValidator : LocalizedValidator<GetPanelLoginDetailsQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPanelLoginDetailsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
