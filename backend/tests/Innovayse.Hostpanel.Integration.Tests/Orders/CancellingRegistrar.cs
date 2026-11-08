using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// A registrar whose registration call is cut short by the caller: the test's cancellation source is cancelled while the
/// call runs, and the call throws the cancellation it observes. Top-level and public so Wolverine's generated handler code
/// can name it; the source is static because the registrar is scoped and a fresh instance serves every handler.
/// </summary>
public sealed class CancellingRegistrar : TestRegistrar
{
    /// <summary>The source the running test cancels, from inside the registration call; set by the test before it dispatches.</summary>
    public static CancellationTokenSource? Source { get; set; }

    /// <inheritdoc/>
    public override Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        Source!.Cancel();
        ct.ThrowIfCancellationRequested();
        return base.RegisterAsync(request, ct);
    }

    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => stub.RenewAsync(request, ct);
}
