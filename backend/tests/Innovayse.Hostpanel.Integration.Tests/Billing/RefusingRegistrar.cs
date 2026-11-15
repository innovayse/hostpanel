using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Domains.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>A registrar that cleanly refuses every renewal.</summary>
/// <param name="time">The clock of the host the registrar answers for.</param>
public sealed class RefusingRegistrar(TimeProvider time) : TestRegistrar(time)
{
    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_ORDER_UPSTREAM_FAILED));
}
