using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>A registrar that renews but, like a purchase response that carries no dates, reports no expiry.</summary>
public sealed class NoExpiryRegistrar : TestRegistrar
{
    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => Task.FromResult(new RegistrarResult(true, request.RegistrarRef, null, null));
}
