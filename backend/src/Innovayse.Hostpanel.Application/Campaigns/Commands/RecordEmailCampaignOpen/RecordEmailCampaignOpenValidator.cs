using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignOpen;

/// <summary>Validates <see cref="RecordEmailCampaignOpenCommand"/>: the token is present and no longer than a real one.</summary>
public sealed class RecordEmailCampaignOpenValidator : LocalizedValidator<RecordEmailCampaignOpenCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecordEmailCampaignOpenValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Token).MustBeCampaignMailToken(L["CampaignMailLinkInvalid"], L["CampaignMailLinkInvalid"]);
    }
}
