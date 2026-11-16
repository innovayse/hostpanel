using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>A registrar with no credentials: it refuses every renewal with its own reason code.</summary>
/// <param name="time">The clock of the host the registrar answers for.</param>
public sealed class NotConfiguredRegistrar(TimeProvider time) : TestRegistrar(time)
{
    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(false, null, null, HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED));
}
