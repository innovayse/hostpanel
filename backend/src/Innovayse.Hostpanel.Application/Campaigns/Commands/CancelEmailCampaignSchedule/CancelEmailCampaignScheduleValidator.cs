using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.CancelEmailCampaignSchedule;

/// <summary>Validates <see cref="CancelEmailCampaignScheduleCommand"/> before it reaches the handler.</summary>
public sealed class CancelEmailCampaignScheduleValidator : LocalizedValidator<CancelEmailCampaignScheduleCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CancelEmailCampaignScheduleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["CampaignIdInvalid"]);
    }
}
