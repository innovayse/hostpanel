using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.ScheduleEmailCampaign;

/// <summary>
/// Validates <see cref="ScheduleEmailCampaignCommand"/> before it reaches the handler. Whether the moment is in the future is a
/// rule of the campaign and is judged there, against the clock.
/// </summary>
public sealed class ScheduleEmailCampaignValidator : LocalizedValidator<ScheduleEmailCampaignCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ScheduleEmailCampaignValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["CampaignIdInvalid"]);
    }
}
