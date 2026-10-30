using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Resolves every fulfilment plugin an admin has switched on, ready to be told about an invoice.
/// Implemented in Infrastructure: reads each plugin's integration settings and instantiates it with a
/// configuration built from them, the same way a payment plugin is activated.
/// </summary>
public interface IFulfilmentPluginResolver
{
    /// <summary>
    /// Resolves all enabled fulfilment plugins. Every one of them sees every digital invoice, so the list
    /// is the set of plugins a paid, refunded or lapsed invoice has to be delivered to.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// One entry per loaded fulfilment plugin that is enabled and has every required setting, in plugin id
    /// order; empty when none is. A plugin that is disabled, misconfigured or does not implement the
    /// fulfilment contract is left out.
    /// </returns>
    Task<IReadOnlyList<ActiveFulfilmentPlugin>> ResolveAllAsync(CancellationToken ct);
}
