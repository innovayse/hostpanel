using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="RegistrarRefusalMap"/>: every outcome and every refusal reason of the SDK has one host code, and a value the host
/// does not know fails closed instead of being read as a success.
/// </summary>
public sealed class RegistrarRefusalMapTests
{
    /// <summary>An outcome that took effect has no failure code.</summary>
    /// <param name="outcome">The outcome.</param>
    [Theory]
    [InlineData(PanelOperationOutcome.Succeeded)]
    [InlineData(PanelOperationOutcome.AlreadyApplied)]
    [InlineData(PanelOperationOutcome.Pending)]
    public void FailureCode_OutcomeThatTookEffect_IsNull(PanelOperationOutcome outcome)
    {
        RegistrarRefusalMap.FailureCode(outcome, null).Should().BeNull();
    }

    /// <summary>A missing or unsupported domain has its own code, whatever reason came with it.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(PanelOperationOutcome.NotFound, HostpanelErrorCodes.REGISTRAR_DOMAIN_NOT_FOUND)]
    [InlineData(PanelOperationOutcome.NotSupported, HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED)]
    public void FailureCode_NotFoundAndNotSupported_HaveTheirOwnCodes(PanelOperationOutcome outcome, string code)
    {
        RegistrarRefusalMap.FailureCode(outcome, RegistrarRefusalReason.Other).Should().Be(code);
    }

    /// <summary>Every refusal reason words as its own host code, and Other is the generic upstream failure.</summary>
    /// <param name="reason">The plugin's reason.</param>
    /// <param name="code">The expected code.</param>
    [Theory]
    [InlineData(RegistrarRefusalReason.DomainUnavailable, HostpanelErrorCodes.REGISTRAR_DOMAIN_UNAVAILABLE)]
    [InlineData(RegistrarRefusalReason.TldNotSupported, HostpanelErrorCodes.REGISTRAR_TLD_NOT_SUPPORTED)]
    [InlineData(RegistrarRefusalReason.EppCodeInvalid, HostpanelErrorCodes.REGISTRAR_EPP_CODE_INVALID)]
    [InlineData(RegistrarRefusalReason.TransferNotAllowed, HostpanelErrorCodes.REGISTRAR_TRANSFER_NOT_ALLOWED)]
    [InlineData(RegistrarRefusalReason.ContactInvalid, HostpanelErrorCodes.REGISTRAR_CONTACT_VALIDATION_FAILED)]
    [InlineData(RegistrarRefusalReason.NameserverInvalid, HostpanelErrorCodes.REGISTRAR_NAMESERVER_INVALID)]
    [InlineData(RegistrarRefusalReason.DnsRecordInvalid, HostpanelErrorCodes.REGISTRAR_DNS_RECORD_INVALID)]
    [InlineData(RegistrarRefusalReason.InsufficientFunds, HostpanelErrorCodes.REGISTRAR_INSUFFICIENT_FUNDS)]
    [InlineData(RegistrarRefusalReason.AccessDenied, HostpanelErrorCodes.REGISTRAR_ACCESS_DENIED)]
    [InlineData(RegistrarRefusalReason.Other, HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED)]
    public void FailureCode_RefusalReason_HasItsOwnCode(RegistrarRefusalReason reason, string code)
    {
        RegistrarRefusalMap.FailureCode(PanelOperationOutcome.Refused, reason).Should().Be(code);
    }

    /// <summary>A refusal with no reason, a reason the host does not know and an outcome it does not know all fail closed.</summary>
    [Fact]
    public void FailureCode_UnknownValues_FailClosed()
    {
        RegistrarRefusalMap.FailureCode(PanelOperationOutcome.Refused, null).Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
        RegistrarRefusalMap.FailureCode(PanelOperationOutcome.Refused, (RegistrarRefusalReason)999).Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
        RegistrarRefusalMap.FailureCode((PanelOperationOutcome)999, null).Should().Be(HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED);
    }
}
