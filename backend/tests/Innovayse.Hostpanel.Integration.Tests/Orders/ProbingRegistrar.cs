using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Integration.Tests.Billing;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// A registrar that lets the test look at the database from inside the registration call, which is where a run is when it
/// has claimed its lines but recorded nothing yet. Top-level and public so Wolverine's generated handler code can name it;
/// the probe is static because the registrar is scoped and a fresh instance serves every handler.
/// </summary>
public sealed class ProbingRegistrar : TestRegistrar
{
    /// <summary>What the running test does while the registration call is in flight; set by the test before it dispatches.</summary>
    public static Func<Task>? Probe { get; set; }

    /// <inheritdoc/>
    public override async Task<RegistrarResult> RegisterAsync(DomainRegistrationOrder request, CancellationToken ct)
    {
        if (Probe is { } probe)
        {
            await probe();
        }

        return await base.RegisterAsync(request, ct);
    }

    /// <inheritdoc/>
    protected override Task<RegistrarResult> Renew(DomainRenewalOrder request, StubRegistrarProvider stub, CancellationToken ct)
        => stub.RenewAsync(request, ct);
}
