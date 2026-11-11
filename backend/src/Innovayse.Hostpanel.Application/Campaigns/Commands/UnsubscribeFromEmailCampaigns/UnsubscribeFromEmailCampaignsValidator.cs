using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Campaigns;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.UnsubscribeFromEmailCampaigns;

/// <summary>
/// Validates <see cref="UnsubscribeFromEmailCampaignsCommand"/>: a token is present and no longer than a real one, so a
/// stranger's oversized input never reaches the lookup.
/// </summary>
public sealed class UnsubscribeFromEmailCampaignsValidator : LocalizedValidator<UnsubscribeFromEmailCampaignsCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UnsubscribeFromEmailCampaignsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage(L["CampaignUnsubscribeTokenRequired"])
            .MaximumLength(EmailCampaignRecipient.UnsubscribeTokenLength).WithMessage(L["CampaignUnsubscribeTokenInvalid"]);
    }
}
