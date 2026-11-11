using FluentValidation;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.CountEmailCampaignAudience;

/// <summary>Validates <see cref="CountEmailCampaignAudienceQuery"/> with the segment rules a campaign is written with.</summary>
public sealed class CountEmailCampaignAudienceValidator : LocalizedValidator<CountEmailCampaignAudienceQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CountEmailCampaignAudienceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Segments).MustBeCampaignSegmentList(
            L["CampaignSegmentsRequired"], L["CampaignSegmentsTooMany", CampaignLimits.MaxSegments], L["CampaignSegmentFilterInvalid"]);
        RuleForEach(x => x.Segments).SetValidator(new EmailCampaignSegmentValidator(localizer)).When(x => x.Segments is not null);
    }
}
