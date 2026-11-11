using FluentValidation;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Campaigns;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.CreateEmailCampaign;

/// <summary>Validates <see cref="CreateEmailCampaignCommand"/> before it reaches the handler.</summary>
public sealed class CreateEmailCampaignValidator : LocalizedValidator<CreateEmailCampaignCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateEmailCampaignValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Subject).MustBeCampaignSubject(L["CampaignSubjectRequired"], L["CampaignSubjectTooLong", EmailCampaign.SubjectMaxLength]);
        RuleFor(x => x.Body).MustBeCampaignBody(L["CampaignBodyRequired"], L["CampaignBodyTooLong", EmailCampaign.BodyMaxLength]);
        RuleFor(x => x.Segments).MustBeCampaignSegmentList(
            L["CampaignSegmentsRequired"], L["CampaignSegmentsTooMany", CampaignLimits.MaxSegments], L["CampaignSegmentFilterInvalid"]);
        RuleForEach(x => x.Segments).SetValidator(new EmailCampaignSegmentValidator(localizer)).When(x => x.Segments is not null);
    }
}
