namespace Innovayse.Hostpanel.Domain.Notifications;

/// <summary>
/// Refusal codes the <see cref="EmailLog"/> entity throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so <c>ResourceCoverageTests</c> requires a sentence for each one in every language.
/// </remarks>
public static class NotificationsErrors
{
    /// <summary>A delivered email cannot be claimed again (409): <see cref="EmailLog.Reclaim"/>.</summary>
    public const string EmailAlreadyDelivered = "EMAIL_ALREADY_DELIVERED";
}
