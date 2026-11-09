using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// A registrar whose registration call waits until the test's other party has reached it too, so two runs are both inside
/// their action, each holding the copy of the order it loaded before either recorded its line. Top-level and public so
/// Wolverine's generated handler code can name it; the gate is static because the registrar is scoped and a fresh instance
/// serves every handler.
/// </summary>
public sealed class RendezvousRegistrar : TestRegistrar
{
    /// <summary>The gate both runs arrive at; set by the running test before it dispatches.</summary>
    public static ChargeRendezvous? Gate { get; set; }

    /// <inheritdoc/>
    public override async Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        await Gate!.ArriveAsync(ct);
        return await base.RegisterAsync(request, ct);
    }

    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => stub.RenewAsync(request, ct);
}
