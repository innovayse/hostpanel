using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place the consent recorded for a hosted payment is ended when that payment will never store a method: the
/// session was declined, was replaced by a new attempt, or its invoice was cancelled or settled another way. The consent
/// is written when the payment is started, before the gateway has stored anything, so without this a consent for a
/// payment that stored nothing would stay open beside the ones that did. A consent that already covers a method is not
/// touched.
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
    public async Task<IReadOnlyList<CardChargeConsent>> RevokeAsync(
        int clientId, string module, string gatewayOrderId, CancellationToken ct)
    {
        var open = await _context.CardChargeConsents
            .Where(c => c.ClientId == clientId && c.GatewayModuleId == module && c.SessionId == gatewayOrderId
                && c.MethodRef == null && c.RevokedAt == null)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        foreach (var consent in open)
        {
            consent.RevokeUnbound(now);
        }

        return open;
    }

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

        return invoice is { GatewayModule: { } module, GatewayOrderId: { } orderId }
            ? RevokeAsync(invoice.ClientId, module, orderId, ct)
            : Task.FromResult<IReadOnlyList<CardChargeConsent>>([]);
    }
}
