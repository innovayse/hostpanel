using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.StartEmailCampaign;

/// <summary>Validates <see cref="StartEmailCampaignCommand"/> before it reaches the handler.</summary>
public sealed class StartEmailCampaignValidator : LocalizedValidator<StartEmailCampaignCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public StartEmailCampaignValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["CampaignIdInvalid"]);
    }
}
