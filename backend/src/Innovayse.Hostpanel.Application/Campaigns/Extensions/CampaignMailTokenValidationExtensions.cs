using FluentValidation;
using Innovayse.Hostpanel.Domain.Campaigns;

namespace Innovayse.Hostpanel.Application.Campaigns.Extensions;

/// <summary>The rule the public validators share about the token a campaign mail's links carry (unsubscribe and tracking tokens have one shape).</summary>
public static class CampaignMailTokenValidationExtensions
{
    /// <summary>
    /// Requires a token: present and no longer than a real one, so a stranger's oversized input never reaches the lookup.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder of the token.</param>
    /// <param name="requiredMessage">The localized sentence reported when the token is missing.</param>
    /// <param name="invalidMessage">The localized sentence reported when the token is longer than a real one.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeCampaignMailToken<T>(
        this IRuleBuilder<T, string> rule, string requiredMessage, string invalidMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .NotEmpty().WithMessage(requiredMessage)
            .MaximumLength(EmailCampaignRecipient.UnsubscribeTokenLength).WithMessage(invalidMessage);
    }
}
