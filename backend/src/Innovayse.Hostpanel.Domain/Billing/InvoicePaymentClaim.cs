using System.Linq.Expressions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The host's claim on one invoice while a charge against a stored payment method is in flight: it is the per invoice
/// gate (an invoice with a blocking claim is never charged again) and the record that finds a charge after a
/// timeout or a crash. It is written, with its idempotency key, before the plugin is called. Stored in the
/// <c>InvoicePaymentClaims</c> table.
/// </summary>
/// <remarks>
/// An unresolved claim (<see cref="IsUnresolved"/>) ends when the plugin's answer, or its key lookup, says how the
/// charge ended. A retry of the same attempt reuses the key; a new attempt after a confirmed failure writes a new
/// claim with a new key. A held claim (<see cref="InvoicePaymentClaimStatus.Held"/>) is final but still blocks: the
/// gateway took money the invoice could not record, so charging the invoice again would take it twice until an
/// operator has settled the held money (<see cref="IsBlocking"/>).
/// </remarks>
public sealed class InvoicePaymentClaim : BaseEntity, IAggregateRoot
{
    /// <summary>The longest text column (module id, method, key, transaction id) the table holds, in characters.</summary>
    public const int MaxLength = 200;

    /// <summary>The longest action address the claim keeps, in characters.</summary>
    public const int MaxActionUrlLength = 2000;

    /// <summary>The longest idempotency key the stored credential capability allows, in characters.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>
    /// How long an unresolved claim is left alone before the reconciliation pass asks the plugin about its key. The
    /// wait lets a charge that is still in flight finish, so a key the gateway has not seen yet is not read as
    /// "never charged".
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    /// <summary>The statuses that count as unresolved.</summary>
    private static readonly InvoicePaymentClaimStatus[] UnresolvedStatuses =
    [
        InvoicePaymentClaimStatus.Open,
        InvoicePaymentClaimStatus.ActionRequired,
        InvoicePaymentClaimStatus.Pending,
    ];

    /// <summary>The statuses that block a new charge of the invoice: the unresolved ones and a held claim.</summary>
    private static readonly InvoicePaymentClaimStatus[] BlockingStatuses =
    [
        InvoicePaymentClaimStatus.Open,
        InvoicePaymentClaimStatus.ActionRequired,
        InvoicePaymentClaimStatus.Pending,
        InvoicePaymentClaimStatus.Held,
    ];

    /// <summary>Gets the invoice the charge is for.</summary>
    public int InvoiceId { get; private set; }

    /// <summary>Gets the client whose method is charged.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the id of the payment plugin that is asked to charge.</summary>
    public string GatewayModuleId { get; private set; } = string.Empty;

    /// <summary>Gets the stored method charged.</summary>
    public string MethodRef { get; private set; } = string.Empty;

    /// <summary>Gets the idempotency key of this attempt, opaque ASCII of at most <see cref="MaxKeyLength"/> characters.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Gets the amount asked of the gateway, in minor units of <see cref="Currency"/>.</summary>
    public long AmountMinor { get; private set; }

    /// <summary>Gets the ISO 4217 code of the invoice's currency, in which the amount is charged.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets where the charge stands.</summary>
    public InvoicePaymentClaimStatus Status { get; private set; }

    /// <summary>Gets the gateway's transaction reference once it took money; otherwise <see langword="null"/>.</summary>
    public string? TransactionId { get; private set; }

    /// <summary>Gets the address the payer authenticates at while the claim is <see cref="InvoicePaymentClaimStatus.ActionRequired"/>.</summary>
    public string? ActionUrl { get; private set; }

    /// <summary>Gets when the claim was written (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets when the reconciliation pass last asked the plugin about the claim's key (UTC), or <see langword="null"/> if it never did.</summary>
    public DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>Gets when the claim reached a final status (UTC), or <see langword="null"/> while it is unresolved.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Gets whether the charge has not ended: the invoice stays claimed.</summary>
    public bool IsUnresolved => UnresolvedStatuses.Contains(Status);

    /// <summary>Gets the filter for the claims that have not ended. The query form of <see cref="IsUnresolved"/>.</summary>
    public static Expression<Func<InvoicePaymentClaim, bool>> UnresolvedFilter =>
        c => c.Status == InvoicePaymentClaimStatus.Open
            || c.Status == InvoicePaymentClaimStatus.ActionRequired
            || c.Status == InvoicePaymentClaimStatus.Pending;

    /// <summary>
    /// Gets whether the claim stops the invoice from being charged again: it is unresolved, or held for an operator
    /// because the gateway took money the invoice could not record.
    /// </summary>
    public bool IsBlocking => BlockingStatuses.Contains(Status);

    /// <summary>Gets the filter for the claims that block a new charge. The query form of <see cref="IsBlocking"/>.</summary>
    public static Expression<Func<InvoicePaymentClaim, bool>> BlockingFilter =>
        c => c.Status == InvoicePaymentClaimStatus.Open
            || c.Status == InvoicePaymentClaimStatus.ActionRequired
            || c.Status == InvoicePaymentClaimStatus.Pending
            || c.Status == InvoicePaymentClaimStatus.Held;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private InvoicePaymentClaim() { }

    /// <summary>Claims an invoice for one stored charge attempt, before the plugin is called.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="clientId">The client whose method is charged.</param>
    /// <param name="gatewayModuleId">The payment plugin id.</param>
    /// <param name="methodRef">The stored method to charge.</param>
    /// <param name="idempotencyKey">The key of this attempt, created once.</param>
    /// <param name="amountMinor">The amount in minor units; positive.</param>
    /// <param name="currency">The invoice's ISO 4217 currency code.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns>A new, unsaved claim in status <see cref="InvoicePaymentClaimStatus.Open"/>.</returns>
    /// <exception cref="ArgumentException">A text argument is empty or too long.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="amountMinor"/> is not positive.</exception>
    public static InvoicePaymentClaim Open(
        int invoiceId,
        int clientId,
        string gatewayModuleId,
        string methodRef,
        string idempotencyKey,
        long amountMinor,
        string currency,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(idempotencyKey.Length, MaxKeyLength);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amountMinor, 0L);

        return new InvoicePaymentClaim
        {
            InvoiceId = invoiceId,
            ClientId = clientId,
            GatewayModuleId = gatewayModuleId,
            MethodRef = methodRef,
            IdempotencyKey = idempotencyKey,
            AmountMinor = amountMinor,
            Currency = currency,
            Status = InvoicePaymentClaimStatus.Open,
            CreatedAt = now,
        };
    }

    /// <summary>Whether the reconciliation pass should ask the plugin about the claim at <paramref name="now"/>.</summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns><see langword="true"/> when the claim is unresolved and was last looked at at least <see cref="StaleAfter"/> ago.</returns>
    public bool IsStale(DateTimeOffset now) => IsUnresolved && (CheckedAt ?? CreatedAt) + StaleAfter <= now;

    /// <summary>Records that the reconciliation pass asked the plugin about the claim's key.</summary>
    /// <param name="now">The current moment (UTC).</param>
    public void RecordCheck(DateTimeOffset now)
    {
        CheckedAt = now;
    }

    /// <summary>
    /// Records that the payer must authenticate before the charge completes. The client is told once, when the claim
    /// first reaches this status; a later report of the same status only refreshes the address.
    /// </summary>
    /// <param name="actionUrl">Where to send the payer; may be <see langword="null"/> when the plugin gave none.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="InvalidOperationException">The claim is already final.</exception>
    public void MarkActionRequired(string? actionUrl, DateTimeOffset now)
    {
        EnsureUnresolved();
        var first = Status != InvoicePaymentClaimStatus.ActionRequired;
        Status = InvoicePaymentClaimStatus.ActionRequired;
        ActionUrl = Truncate(actionUrl, MaxActionUrlLength);
        if (first)
        {
            AddDomainEvent(() => new StoredChargeActionRequiredEvent(Id, InvoiceId, ClientId, ActionUrl) { OccurredAt = now });
        }
    }

    /// <summary>Records that the gateway accepted the charge but has not settled it.</summary>
    /// <exception cref="InvalidOperationException">The claim is already final.</exception>
    public void MarkPending()
    {
        EnsureUnresolved();
        Status = InvoicePaymentClaimStatus.Pending;
        ActionUrl = null;
    }

    /// <summary>Records that the money was taken and recorded on the invoice.</summary>
    /// <param name="transactionId">The gateway's transaction reference.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="ArgumentException"><paramref name="transactionId"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">The claim is already final.</exception>
    public void MarkSettled(string transactionId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        Resolve(InvoicePaymentClaimStatus.Settled, transactionId, now);
    }

    /// <summary>Records that the gateway refused the charge.</summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="InvalidOperationException">The claim is already final.</exception>
    public void MarkDeclined(DateTimeOffset now)
    {
        Resolve(InvoicePaymentClaimStatus.Declined, null, now);
        AddDomainEvent(() => new StoredChargeDeclinedEvent(Id, InvoiceId, ClientId) { OccurredAt = now });
    }

    /// <summary>Records that the gateway confirmed it never charged under the claim's key.</summary>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="InvalidOperationException">The claim is already final.</exception>
    public void MarkAbandoned(DateTimeOffset now)
    {
        Resolve(InvoicePaymentClaimStatus.Abandoned, null, now);
    }

    /// <summary>Records that the gateway took money the invoice could not record, for an operator to settle by hand.</summary>
    /// <param name="transactionId">The gateway's transaction reference, or <see langword="null"/> when it gave none.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <exception cref="InvalidOperationException">The claim is already final.</exception>
    public void MarkHeld(string? transactionId, DateTimeOffset now)
    {
        Resolve(InvoicePaymentClaimStatus.Held, transactionId, now);
    }

    /// <summary>Moves the claim to a final status.</summary>
    /// <param name="status">The final status.</param>
    /// <param name="transactionId">The transaction reference to keep, if any.</param>
    /// <param name="now">The current moment (UTC).</param>
    private void Resolve(InvoicePaymentClaimStatus status, string? transactionId, DateTimeOffset now)
    {
        EnsureUnresolved();
        Status = status;
        TransactionId = Truncate(transactionId, MaxLength);
        ActionUrl = null;
        ResolvedAt = now;
    }

    /// <summary>Refuses a change to a claim that already ended.</summary>
    /// <exception cref="InvalidOperationException">The claim is final.</exception>
    private void EnsureUnresolved()
    {
        if (!IsUnresolved)
        {
            throw new InvalidOperationException($"The invoice payment claim is already {Status}.");
        }
    }

    /// <summary>Cuts a text to a column's width.</summary>
    /// <param name="value">The text, or <see langword="null"/>.</param>
    /// <param name="max">The width.</param>
    /// <returns>The text, no longer than <paramref name="max"/>, or <see langword="null"/>.</returns>
    private static string? Truncate(string? value, int max) =>
        value is not null && value.Length > max ? value[..max] : value;
}
