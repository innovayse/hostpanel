using Innovayse.Hostpanel.Application.Domains.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>A registrar whose renewal call throws, as a timeout does: the outcome is unknown.</summary>
/// <param name="time">The clock of the host the registrar answers for.</param>
public sealed class ThrowingRegistrar(TimeProvider time) : TestRegistrar(time)
{
    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => throw new HttpRequestException("registrar timed out");
}
