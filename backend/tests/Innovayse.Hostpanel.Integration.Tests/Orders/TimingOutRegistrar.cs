using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// A registrar whose registration call times out the way an HTTP client does: it throws a cancellation the caller never
/// asked for, so the outcome at the registrar is unknown. Top-level and public so Wolverine's generated handler code can name it.
/// </summary>
public sealed class TimingOutRegistrar : TestRegistrar
{
    /// <inheritdoc/>
    public override Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
        => throw new TaskCanceledException("registrar timed out");

    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => stub.RenewAsync(request, ct);
}
