using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// A client's request to move one of their services to another plan of the same product group, with the
/// prorated amount fixed at the moment it was made. Linked to a <see cref="ClientService"/> via <see cref="ServiceId"/>.
/// Stored in the <c>ServiceChangeRequests</c> table.
/// </summary>
/// <remarks>
/// Lifecycle: <see cref="ServiceChangeStatus.Pending"/> to <see cref="ServiceChangeStatus.Approved"/> to
/// <see cref="ServiceChangeStatus.Completed"/>, or to <see cref="ServiceChangeStatus.Rejected"/> from either of the first two.
/// Every other move throws, so a request cannot be completed twice or rejected after it was applied.
/// </remarks>
public sealed class ServiceChangeRequest : BaseEntity
{
    /// <summary>The longest staff note the <see cref="Note"/> column holds, in characters.</summary>
    public const int MaxNoteLength = 2000;

    /// <summary>Gets the FK to the service whose plan changes.</summary>
    public int ServiceId { get; private set; }

    /// <summary>Gets the FK to the client who owns the service and is charged or credited.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the FK to the product the service runs on when the request is made.</summary>
    public int FromProductId { get; private set; }

    /// <summary>Gets the FK to the product the service moves to.</summary>
    public int ToProductId { get; private set; }

    /// <summary>Gets whether the change is an upgrade or a downgrade.</summary>
    public ServiceChangeType Type { get; private set; }

    /// <summary>Gets the current lifecycle status.</summary>
    public ServiceChangeStatus Status { get; private set; }

    /// <summary>Gets the prorated difference in <see cref="CurrencyCode"/>: the charge of an upgrade, the credit of a downgrade.</summary>
    public decimal ProrationAmount { get; private set; }

    /// <summary>Gets the ISO 4217 alpha code of the client's billing currency the amounts are in.</summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    /// <summary>Gets the new plan's recurring price per cycle, which the service is billed from the change on.</summary>
    public decimal NewRecurringAmount { get; private set; }

    /// <summary>Gets the proration invoice of an upgrade; an index only, the invoice belongs to Billing and has no foreign key here.</summary>
    public int? InvoiceId { get; private set; }

    /// <summary>Gets the staff note given when the request was approved or rejected.</summary>
    public string? Note { get; private set; }

    /// <summary>Gets when the request was made (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets when the request was completed or rejected (UTC); <see langword="null"/> while it is open.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Gets a value indicating whether the request still blocks another one for the same service.</summary>
    public bool IsOpen => Status is ServiceChangeStatus.Pending or ServiceChangeStatus.Approved;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ServiceChangeRequest() { }

    /// <summary>Creates a pending request.</summary>
    /// <param name="serviceId">FK to the service whose plan changes.</param>
    /// <param name="clientId">FK to the client who owns the service.</param>
    /// <param name="fromProductId">FK to the product the service runs on now.</param>
    /// <param name="toProductId">FK to the product the service moves to.</param>
    /// <param name="change">The type and prorated amount worked out for the move.</param>
    /// <param name="currencyCode">ISO 4217 alpha code of the client's billing currency.</param>
    /// <param name="newRecurringAmount">The new plan's recurring price per cycle, in the same currency.</param>
    /// <param name="now">The moment the request is made (UTC).</param>
    /// <returns>A new pending <see cref="ServiceChangeRequest"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An amount is negative.</exception>
    /// <exception cref="ArgumentException">The two products are the same, or the currency code is empty.</exception>
    public static ServiceChangeRequest Create(
        int serviceId, int clientId, int fromProductId, int toProductId, ProratedPlanChange change, string currencyCode,
        decimal newRecurringAmount, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentOutOfRangeException.ThrowIfNegative(change.Amount);
        ArgumentOutOfRangeException.ThrowIfNegative(newRecurringAmount);
        if (fromProductId == toProductId)
        {
            throw new ArgumentException("A plan change moves a service to another product.", nameof(toProductId));
        }

        return new ServiceChangeRequest
        {
            ServiceId = serviceId,
            ClientId = clientId,
            FromProductId = fromProductId,
            ToProductId = toProductId,
            Type = change.Type,
            Status = ServiceChangeStatus.Pending,
            ProrationAmount = change.Amount,
            CurrencyCode = currencyCode.ToUpperInvariant(),
            NewRecurringAmount = newRecurringAmount,
            CreatedAt = now,
        };
    }

    /// <summary>Approves a pending request.</summary>
    /// <param name="note">An optional staff note, kept with the request; <see langword="null"/> for none.</param>
    /// <exception cref="DomainRuleException">The request is not pending.</exception>
    public void Approve(string? note = null)
    {
        EnsureStatus(ServiceChangeStatus.Pending, "approved");
        Status = ServiceChangeStatus.Approved;
        Note = note;
    }

    /// <summary>Records the proration invoice an approved upgrade waits for.</summary>
    /// <param name="invoiceId">The invoice the client pays before the plan changes.</param>
    /// <exception cref="DomainRuleException">The request is not approved.</exception>
    public void AttachInvoice(int invoiceId)
    {
        EnsureStatus(ServiceChangeStatus.Approved, "given an invoice");
        InvoiceId = invoiceId;
    }

    /// <summary>Marks an approved request as applied: the service runs on the new plan.</summary>
    /// <param name="now">The moment the change was applied (UTC).</param>
    /// <exception cref="DomainRuleException">The request is not approved.</exception>
    public void Complete(DateTimeOffset now)
    {
        EnsureStatus(ServiceChangeStatus.Approved, "completed");
        Status = ServiceChangeStatus.Completed;
        ResolvedAt = now;
    }

    /// <summary>Turns a pending or approved request down; a completed request cannot be taken back.</summary>
    /// <param name="note">An optional staff note, kept with the request; <see langword="null"/> keeps the one already there.</param>
    /// <param name="now">The moment the request was rejected (UTC).</param>
    /// <exception cref="DomainRuleException">The request is already completed or rejected.</exception>
    public void Reject(string? note, DateTimeOffset now)
    {
        if (!IsOpen)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceChangeInvalidState, $"A {Status} plan change request cannot be rejected.");
        }

        Status = ServiceChangeStatus.Rejected;
        Note = note ?? Note;
        ResolvedAt = now;
    }

    /// <summary>Refuses a transition from any status but the expected one.</summary>
    /// <param name="expected">The only status the move is allowed from.</param>
    /// <param name="action">What the request was asked to become, for the message.</param>
    /// <exception cref="DomainRuleException">The request is in another status.</exception>
    private void EnsureStatus(ServiceChangeStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainRuleException(
                ServicesErrors.ServiceChangeInvalidState, $"A {Status} plan change request cannot be {action}.");
        }
    }
}
