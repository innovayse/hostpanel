using FluentValidation;
using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignClick;

/// <summary>Validates <see cref="RecordEmailCampaignClickCommand"/>: the token is present and no longer than a real one, and the link index is not negative.</summary>
public sealed class RecordEmailCampaignClickValidator : LocalizedValidator<RecordEmailCampaignClickCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecordEmailCampaignClickValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Token).MustBeCampaignMailToken(L["CampaignMailLinkInvalid"], L["CampaignMailLinkInvalid"]);
        RuleFor(x => x.LinkIndex).GreaterThanOrEqualTo(0).WithMessage(L["CampaignMailLinkInvalid"]);
    }
}
