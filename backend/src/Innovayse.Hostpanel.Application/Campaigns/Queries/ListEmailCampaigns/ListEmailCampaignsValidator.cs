using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.ListEmailCampaigns;

/// <summary>
/// Validates <see cref="ListEmailCampaignsQuery"/> before it reaches the handler. The page, page size and search are the
/// shared list rules (<see cref="ListQueryValidator{T}"/>); this adds the rule of the status filter.
/// </summary>
public sealed class ListEmailCampaignsValidator : ListQueryValidator<ListEmailCampaignsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListEmailCampaignsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["CampaignListStatusInvalid"]);
    }
}
