using System.Globalization;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The exclusive right to charge one invoice's card, taken before any money moves and held until the
/// attempt ends. Stored in the <c>InvoicePaymentClaims</c> table, where a unique index allows one
/// unfinished claim per invoice.
/// </summary>
/// <remarks>
/// Without it the renewal job and the client could each charge the same invoice at the same moment,
/// under different gateway keys, and both charges would take money. The claim's key is the
/// idempotency key of the gateway call, so even a retry of one claim cannot charge twice. Ended claims
/// stay as the history of the invoice's payment attempts.
/// </remarks>
public sealed class InvoicePaymentClaim : BaseEntity
{
    /// <summary>Gets the invoice being paid.</summary>
    public int InvoiceId { get; private set; }

    /// <summary>Gets who is paying.</summary>
    public InvoicePaymentClaimKind Kind { get; private set; }

    /// <summary>Gets the idempotency key the gateway call of this claim is sent with and looked up by.</summary>
    public string GatewayIdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Gets when the claim was taken.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Gets when the claim stops counting as held, if its holder never reports back.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets how the claim ended, or <see langword="null"/> while it is still held.</summary>
    public InvoicePaymentClaimOutcome? Outcome { get; private set; }

    /// <summary>Gets when the claim ended.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private InvoicePaymentClaim() { }

    /// <summary>Takes the right to charge an invoice.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="kind">Who is paying.</param>
    /// <param name="gatewayIdempotencyKey">The key the gateway call of this claim is sent with: the client's own payment attempt for a manual payment, <see cref="AutomaticKey"/> for an automatic one.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="lifetime">How long the claim counts as held when its holder never reports back.</param>
    /// <returns>A new, held claim.</returns>
    public static InvoicePaymentClaim Start(
        int invoiceId, InvoicePaymentClaimKind kind, string gatewayIdempotencyKey, DateTimeOffset now, TimeSpan lifetime) =>
        new()
        {
            InvoiceId = invoiceId,
            Kind = kind,
            GatewayIdempotencyKey = gatewayIdempotencyKey,
            StartedAt = now,
            ExpiresAt = now + lifetime,
        };

    /// <summary>Builds the gateway key of an automatic charge, different for every claim of the invoice.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="token">A value unique to the claim.</param>
    /// <returns>The key the automatic charge is sent and looked up with.</returns>
    public static string AutomaticKey(int invoiceId, Guid token) =>
        string.Create(CultureInfo.InvariantCulture, $"invoice-{invoiceId}-auto-{token:N}");

    /// <summary>Whether the claim is still held but its time has run out.</summary>
    /// <param name="now">The current moment.</param>
    /// <returns><see langword="true"/> when it has no outcome and <see cref="ExpiresAt"/> has passed.</returns>
    public bool IsStale(DateTimeOffset now) => Outcome is null && ExpiresAt <= now;

    /// <summary>Ends the claim: the charge was taken and the invoice settled.</summary>
    /// <param name="now">The current moment.</param>
    public void MarkSettled(DateTimeOffset now) => End(InvoicePaymentClaimOutcome.Settled, now);

    /// <summary>Ends the claim: the attempt took no money.</summary>
    /// <param name="now">The current moment.</param>
    public void MarkFailed(DateTimeOffset now) => End(InvoicePaymentClaimOutcome.Failed, now);

    /// <summary>Ends a stale claim whose attempt the gateway never took money for.</summary>
    /// <param name="now">The current moment.</param>
    public void MarkExpired(DateTimeOffset now) => End(InvoicePaymentClaimOutcome.Expired, now);

    /// <summary>Records how the claim ended; an ended claim is never changed again.</summary>
    /// <param name="outcome">How it ended.</param>
    /// <param name="now">The current moment.</param>
    private void End(InvoicePaymentClaimOutcome outcome, DateTimeOffset now)
    {
        if (Outcome is not null)
        {
            return;
        }

        Outcome = outcome;
        CompletedAt = now;
    }
}
