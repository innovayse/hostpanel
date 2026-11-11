using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Campaigns;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// Validates one segment of a campaign: the filter is a known one and its value fits it, as the campaign itself would
/// judge it (<see cref="EmailCampaignSegment.Accepts"/>); shared by the create and update validators.
/// </summary>
public sealed class EmailCampaignSegmentValidator : LocalizedValidator<CampaignSegmentDefinition>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public EmailCampaignSegmentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.FilterType).IsInEnum().WithMessage(L["CampaignSegmentFilterInvalid"]);
        RuleFor(x => x.FilterValue)
            .Must((segment, value) => EmailCampaignSegment.Accepts(segment.FilterType, value))
            .WithMessage(L["CampaignSegmentValueInvalid"])
            .When(x => Enum.IsDefined(x.FilterType));
    }
}
