using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.ListEmailCampaigns;

/// <summary>Validates <see cref="ListEmailCampaignsQuery"/> before it reaches the handler. The query carries no input, so there are no rules.</summary>
public sealed class ListEmailCampaignsValidator : LocalizedValidator<ListEmailCampaignsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListEmailCampaignsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
