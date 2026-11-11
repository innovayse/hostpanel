using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;

/// <summary>Validates <see cref="DeliverEmailCampaignCommand"/> before it reaches the handler.</summary>
public sealed class DeliverEmailCampaignValidator : LocalizedValidator<DeliverEmailCampaignCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeliverEmailCampaignValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["CampaignIdInvalid"]);
    }
}
