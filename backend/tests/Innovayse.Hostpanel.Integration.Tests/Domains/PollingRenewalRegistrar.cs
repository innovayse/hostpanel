using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>A registrar that accepts a renewal order but has not finished it, so the answer asks the host to poll.</summary>
/// <param name="time">The clock of the host the registrar answers for.</param>
public sealed class PollingRenewalRegistrar(TimeProvider time) : TestRegistrar(time)
{
    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, request.RegistrarRef, request.CurrentExpiresAt?.AddYears(request.Years), null, true));
}
