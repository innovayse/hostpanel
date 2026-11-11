using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.PreviewEmailCampaign;

/// <summary>Validates <see cref="PreviewEmailCampaignQuery"/> before it reaches the handler.</summary>
public sealed class PreviewEmailCampaignValidator : LocalizedValidator<PreviewEmailCampaignQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public PreviewEmailCampaignValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["CampaignIdInvalid"]);
    }
}
