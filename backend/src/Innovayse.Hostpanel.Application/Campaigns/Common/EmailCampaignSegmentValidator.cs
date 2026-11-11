using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// Validates one segment of a campaign: the filter is a known one and its value fits it, as the campaign itself would
/// judge it (<see cref="EmailCampaignSegment.Accepts"/>); shared by the create, update and count validators. A filter on
/// the last sign-in is refused when the deployment does not track it.
/// </summary>
public sealed class EmailCampaignSegmentValidator : LocalizedValidator<CampaignSegmentDto>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    /// <param name="authMode">
    /// Tells whether this deployment owns its accounts: only then is the last sign-in known, because a sign-on provider
    /// tracks sign-ins to itself and not to this product.
    /// </param>
    public EmailCampaignSegmentValidator(IStringLocalizer<ApplicationValidationMessages> localizer, IAuthModeProvider authMode)
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(authMode);

        RuleFor(x => x.FilterType).IsInEnum().WithMessage(L["CampaignSegmentFilterInvalid"]);
        RuleFor(x => x.FilterValue)
            .Must((segment, value) => EmailCampaignSegment.Accepts(segment.FilterType, value))
            .WithMessage(L["CampaignSegmentValueInvalid"])
            .When(x => Enum.IsDefined(x.FilterType));
        RuleFor(x => x.FilterType)
            .Must(_ => authMode.IsLocalMode)
            .WithMessage(L["CampaignSegmentSignInUnavailable"])
            .When(x => x.FilterType == CampaignFilterType.NotSignedInForDays);
    }
}
