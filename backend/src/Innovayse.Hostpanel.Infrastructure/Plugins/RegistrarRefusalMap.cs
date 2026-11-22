using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The one table that turns what a registrar plugin answered into the host's error code. Only the outcome and the reason
/// enum are read: the plugin's own words never reach a response, and a value the host does not know fails closed.
/// </summary>
public static class RegistrarRefusalMap
{
    /// <summary>Maps a plugin's answer to a host error code.</summary>
    /// <param name="outcome">How the operation ended.</param>
    /// <param name="reason">The refusal reason, read only when <paramref name="outcome"/> is a refusal.</param>
    /// <returns>
    /// <see langword="null"/> when the operation took effect (done, already done, or accepted and still pending), otherwise the
    /// <c>REGISTRAR_*</c> code that words why it did not.
    /// </returns>
    public static string? FailureCode(PanelOperationOutcome outcome, RegistrarRefusalReason? reason)
    {
        return outcome switch
        {
            PanelOperationOutcome.Succeeded or PanelOperationOutcome.AlreadyApplied or PanelOperationOutcome.Pending => null,
            PanelOperationOutcome.NotFound => HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND,
            PanelOperationOutcome.NotSupported => HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED,
            PanelOperationOutcome.Refused => ReasonCode(reason),
            _ => HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED,
        };
    }

    /// <summary>Maps a refusal reason to a host error code.</summary>
    /// <param name="reason">The plugin's reason; a missing or undefined one counts as <see cref="RegistrarRefusalReason.Other"/>.</param>
    /// <returns>The <c>REGISTRAR_*</c> code that words the refusal.</returns>
    private static string ReasonCode(RegistrarRefusalReason? reason)
    {
        return reason switch
        {
            RegistrarRefusalReason.DomainUnavailable => HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE,
            RegistrarRefusalReason.TldNotSupported => HostpanelErrorCodes.REGISTRAR_TLD_NOT_SUPPORTED,
            RegistrarRefusalReason.EppCodeInvalid => HostpanelErrorCodes.REGISTRAR_EPP_CODE_INVALID,
            RegistrarRefusalReason.TransferNotAllowed => HostpanelErrorCodes.REGISTRAR_TRANSFER_NOT_ALLOWED,
            RegistrarRefusalReason.ContactInvalid => HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED,
            RegistrarRefusalReason.NameserverInvalid => HostpanelErrorCodes.REGISTRAR_NAMESERVER_INVALID,
            RegistrarRefusalReason.DnsRecordInvalid => HostpanelErrorCodes.REGISTRAR_DNS_RECORD_INVALID,
            RegistrarRefusalReason.InsufficientFunds => HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS,
            RegistrarRefusalReason.AccessDenied => HostpanelErrorCodes.REGISTRAR_ACCESS_DENIED,
            _ => HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED,
        };
    }
}
