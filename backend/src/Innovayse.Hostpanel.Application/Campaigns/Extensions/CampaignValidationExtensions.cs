using FluentValidation;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Domain.Campaigns;

namespace Innovayse.Hostpanel.Application.Campaigns.Extensions;

/// <summary>
/// The rules the create and update validators share about what a campaign is written with, so the two never drift apart.
/// The length limits are the stored columns' (<see cref="EmailCampaign"/>), and a segment's value is judged by the domain.
/// </summary>
public static class CampaignValidationExtensions
{
    /// <summary>Requires a subject line: not blank, at most <see cref="EmailCampaign.SubjectMaxLength"/> characters.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder of the subject.</param>
    /// <param name="requiredMessage">The localized sentence reported when the subject is blank.</param>
    /// <param name="tooLongMessage">The localized sentence reported when the subject is longer than its column.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeCampaignSubject<T>(
        this IRuleBuilder<T, string> rule, string requiredMessage, string tooLongMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(subject => !string.IsNullOrWhiteSpace(subject)).WithMessage(requiredMessage)
            .MaximumLength(EmailCampaign.SubjectMaxLength).WithMessage(tooLongMessage);
    }

    /// <summary>Requires a body: not blank, at most <see cref="EmailCampaign.BodyMaxLength"/> characters.</summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder of the body.</param>
    /// <param name="requiredMessage">The localized sentence reported when the body is blank.</param>
    /// <param name="tooLongMessage">The localized sentence reported when the body is longer than its column.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> MustBeCampaignBody<T>(
        this IRuleBuilder<T, string> rule, string requiredMessage, string tooLongMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .Must(body => !string.IsNullOrWhiteSpace(body)).WithMessage(requiredMessage)
            .MaximumLength(EmailCampaign.BodyMaxLength).WithMessage(tooLongMessage);
    }

    /// <summary>
    /// Requires a segment list that is present, holds no empty entry and has at most <see cref="CampaignLimits.MaxSegments"/>
    /// entries. It may be empty: a draft is written before its audience is chosen. Each entry is checked by
    /// <see cref="EmailCampaignSegmentValidator"/>, which the validator attaches with <c>RuleForEach</c>.
    /// </summary>
    /// <typeparam name="T">The command being validated.</typeparam>
    /// <param name="rule">The rule builder of the segment list.</param>
    /// <param name="requiredMessage">The localized sentence reported when the list is missing.</param>
    /// <param name="tooManyMessage">The localized sentence reported when the list is longer than <see cref="CampaignLimits.MaxSegments"/>.</param>
    /// <param name="emptyEntryMessage">The localized sentence reported when an entry of the list is missing.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, IReadOnlyList<CampaignSegmentDefinition>> MustBeCampaignSegmentList<T>(
        this IRuleBuilder<T, IReadOnlyList<CampaignSegmentDefinition>> rule, string requiredMessage, string tooManyMessage, string emptyEntryMessage)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return rule
            .NotNull().WithMessage(requiredMessage)
            .Must(segments => segments is null || segments.Count <= CampaignLimits.MaxSegments).WithMessage(tooManyMessage)
            .Must(segments => segments is null || segments.All(segment => segment is not null)).WithMessage(emptyEntryMessage);
    }
}
