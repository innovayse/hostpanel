using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DispatchEmailCampaignsCron;

/// <summary>Validates <see cref="DispatchEmailCampaignsCronCommand"/>: no rules; the command carries no input.</summary>
public sealed class DispatchEmailCampaignsCronValidator : LocalizedValidator<DispatchEmailCampaignsCronCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DispatchEmailCampaignsCronValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
