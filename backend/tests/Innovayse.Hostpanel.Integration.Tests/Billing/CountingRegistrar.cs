using Innovayse.Hostpanel.Application.Domains.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>A registrar that renews like the stub, and only counts the calls.</summary>
/// <param name="time">The clock of the host the registrar answers for.</param>
public sealed class CountingRegistrar(TimeProvider time) : TestRegistrar(time)
{
    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => stub.RenewAsync(request, ct);
}
