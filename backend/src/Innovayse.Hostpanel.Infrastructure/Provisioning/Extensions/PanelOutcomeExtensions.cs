using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Provisioning.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning.Extensions;

/// <summary>
/// The one mapping from what a panel plugin answers to what the host does with it: used by the provider, the connection tester
/// and the usage reader, so no operation carries a copy of the table.
/// </summary>
public static class PanelOutcomeExtensions
{
    /// <summary>Maps a panel's refusal reason to the host's error code.</summary>
    /// <param name="reason">The reason the plugin gave, or <see langword="null"/> when it gave none.</param>
    /// <returns>
    /// The code that names the reason; <c>PANEL_REFUSED</c> for <see cref="PanelRefusalReason.Other"/>, for a reason the host does
    /// not name yet (the virtual server ones until they are mapped) and for no reason at all.
    /// </returns>
    public static string ToErrorCode(this PanelRefusalReason? reason)
    {
        return reason switch
        {
            PanelRefusalReason.PackageUnknown => HostpanelErrorCodes.PANEL_PACKAGE_UNKNOWN,
            PanelRefusalReason.UsernameInvalid => HostpanelErrorCodes.PANEL_USERNAME_INVALID,
            PanelRefusalReason.DomainInvalid => HostpanelErrorCodes.PANEL_DOMAIN_INVALID,
            PanelRefusalReason.PasswordRejected => HostpanelErrorCodes.PANEL_PASSWORD_REJECTED,
            PanelRefusalReason.LimitReached => HostpanelErrorCodes.PANEL_LIMIT_REACHED,
            PanelRefusalReason.AccessDenied => HostpanelErrorCodes.PANEL_ACCESS_DENIED,
            _ => HostpanelErrorCodes.PANEL_REFUSED,
        };
    }

    /// <summary>Maps the state a panel plugin reports for an account to the host's own.</summary>
    /// <param name="status">The state the plugin reported.</param>
    /// <returns>The host's account status.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is a value the host does not know, such as one a newer SDK adds.</exception>
    public static HostedAccountStatus ToHostedAccountStatus(this PanelAccountStatus status)
    {
        return status switch
        {
            PanelAccountStatus.Active => HostedAccountStatus.Active,
            PanelAccountStatus.Suspended => HostedAccountStatus.Suspended,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The host does not know this account status."),
        };
    }

    /// <summary>
    /// Lets an answer that did the work through and refuses every other one: <see cref="PanelOperationOutcome.Succeeded"/>,
    /// <see cref="PanelOperationOutcome.AlreadyApplied"/> (the state the caller wanted already held) and
    /// <see cref="PanelOperationOutcome.Pending"/> (the panel accepted the work and finishes it on its own) return normally.
    /// </summary>
    /// <param name="outcome">How the plugin answered.</param>
    /// <param name="reason">The refusal reason the plugin gave, when it refused.</param>
    /// <exception cref="DomainRuleException">
    /// Thrown with <c>PANEL_ACCOUNT_NOT_FOUND</c> for <see cref="PanelOperationOutcome.NotFound"/>, <c>PANEL_OPERATION_NOT_SUPPORTED</c>
    /// for <see cref="PanelOperationOutcome.NotSupported"/> and the code of <paramref name="reason"/> for a refusal.
    /// </exception>
    public static void EnsureDone(this PanelOperationOutcome outcome, PanelRefusalReason? reason)
    {
        switch (outcome)
        {
            case PanelOperationOutcome.NotFound:
                throw new DomainRuleException(HostpanelErrorCodes.PANEL_ACCOUNT_NOT_FOUND, "The control panel has no such account.");
            case PanelOperationOutcome.NotSupported:
                throw new DomainRuleException(HostpanelErrorCodes.PANEL_OPERATION_NOT_SUPPORTED, "The control panel does not support the action.");
            case PanelOperationOutcome.Refused:
                throw new DomainRuleException(reason.ToErrorCode(), "The control panel refused the request.");
        }
    }
}
