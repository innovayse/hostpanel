using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place the consent recorded for a hosted payment is ended when that payment will never store a method: the
/// session was declined, was replaced by a new attempt, or its invoice was cancelled or settled another way. The consent
/// is written when the payment is started, before the gateway has stored anything, so without this a consent for a
/// payment that stored nothing would stay open beside the ones that did. A consent that already covers a method is not
/// touched. Every entry point resolves its sessions and goes through one query.
/// </summary>
public sealed class UnboundConsentRevoker
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the revocation is stamped from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="UnboundConsentRevoker"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the revocation is stamped from.</param>
    public UnboundConsentRevoker(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Revokes the client's consent recorded for a gateway session, when it covers no method. Found by the client as well
    /// as the session, the way <see cref="StoredMethodBinder"/> finds it, so a session of someone else's ends nothing.
    /// </summary>
    /// <remarks>Stages the change only: the caller saves, in the save that belongs to its own use case.</remarks>
    /// <param name="clientId">The invoice's client, who gave the consent.</param>
    /// <param name="module">The payment plugin id of the session.</param>
    /// <param name="gatewayOrderId">The gateway-side order of the session, which keys the consent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The consents that were open and are now revoked (tracked), so a caller whose save is lost can drop just them.</returns>
    public Task<IReadOnlyList<CardChargeConsent>> RevokeAsync(
        int clientId, string module, string gatewayOrderId, CancellationToken ct) =>
        RevokeSessionsAsync([(clientId, module, gatewayOrderId)], ct);

    /// <summary>
    /// Revokes the consent of the session an invoice holds, taking the module and the gateway order from the invoice so a
    /// caller that closes the invoice does not repeat them. An invoice with no hosted session ends nothing.
    /// </summary>
    /// <remarks>Stages the change only: the caller saves, in the save that closes the invoice.</remarks>
    /// <param name="invoice">The invoice being closed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The consents that were open and are now revoked (tracked).</returns>
    public Task<IReadOnlyList<CardChargeConsent>> RevokeOfAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return RevokeOfAsync([invoice], ct);
    }

    /// <summary>
    /// Revokes the consents of the sessions of many invoices with one query, for a caller that closes a batch of them.
    /// Invoices with no hosted session end nothing.
    /// </summary>
    /// <remarks>Stages the change only: the caller saves, in the save that closes the invoices.</remarks>
    /// <param name="invoices">The invoices being closed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The consents that were open and are now revoked (tracked).</returns>
    public Task<IReadOnlyList<CardChargeConsent>> RevokeOfAsync(IEnumerable<Invoice> invoices, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var sessions = invoices
            .Where(i => i.GatewayModule is not null && i.GatewayOrderId is not null)
            .Select(i => (i.ClientId, Module: i.GatewayModule!, OrderId: i.GatewayOrderId!))
            .ToList();
        return RevokeSessionsAsync(sessions, ct);
    }

    /// <summary>Finds the open, method-less consents of the given sessions with one query and revokes them.</summary>
    /// <param name="sessions">The client, module and gateway order of each session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The consents that were open and are now revoked (tracked).</returns>
    private async Task<IReadOnlyList<CardChargeConsent>> RevokeSessionsAsync(
        IReadOnlyCollection<(int ClientId, string Module, string OrderId)> sessions, CancellationToken ct)
    {
        if (sessions.Count == 0)
        {
            return [];
        }

        // The database narrows by the gateway order, which is selective; the client and module are matched exactly here.
        var orderIds = sessions.Select(s => s.OrderId).Distinct().ToList();
        var candidates = await _context.CardChargeConsents
            .Where(c => orderIds.Contains(c.SessionId) && c.MethodRef == null && c.RevokedAt == null)
            .ToListAsync(ct);

        var wanted = sessions.ToHashSet();
        var open = candidates.Where(c => wanted.Contains((c.ClientId, c.GatewayModuleId, c.SessionId))).ToList();

        var now = _clock.UtcNow;
        foreach (var consent in open)
        {
            consent.RevokeUnbound(now);
        }

        return open;
    }
}
