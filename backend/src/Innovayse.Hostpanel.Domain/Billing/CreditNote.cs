using System.Globalization;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A document that credits all or part of a paid <see cref="Invoice"/> back to the client, at the invoice's own tax rate.
/// Stored in the <c>CreditNotes</c> table with its lines in <c>CreditNoteItems</c>.
/// </summary>
/// <remarks>
/// <para>
/// A credit note is not an invoice credit: invoice credit is balance a client spends on an invoice, a credit note is a
/// document that reverses part of an invoice that was already paid. Its value goes back either to the client's balance
/// (<see cref="CreditNoteMethod.AccountCredit"/>) or to the payer through the gateway (<see cref="CreditNoteMethod.GatewayRefund"/>).
/// </para>
/// <para>
/// The numbering (<c>CN-0001</c>) is a cautious default and has not been checked against any local law: an operator must
/// confirm what their jurisdiction requires of the numbering, the content and the retention of such a document. A note that has
/// been issued is never deleted, only voided, and it keeps its number.
/// </para>
/// </remarks>
public sealed class CreditNote : BaseEntity, IAggregateRoot
{
    /// <summary>The prefix of every credit note number.</summary>
    public const string NumberPrefix = "CN-";

    /// <summary>The fewest digits a number is padded to; a longer sequence value simply prints longer.</summary>
    public const int NumberDigits = 4;

    /// <summary>The longest reason a note or its voiding keeps.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>Internal mutable list of lines.</summary>
    private readonly List<CreditNoteItem> _items = [];

    /// <summary>Gets the FK to the client the note is issued to.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the FK to the invoice the note credits.</summary>
    public int InvoiceId { get; private set; }

    /// <summary>Gets the note's number such as <c>CN-0001</c>, or null while the note is a <see cref="CreditNoteStatus.Draft"/>.</summary>
    public string? Number { get; private set; }

    /// <summary>Gets the lifecycle state.</summary>
    public CreditNoteStatus Status { get; private set; }

    /// <summary>Gets how the note's value goes back to the client.</summary>
    public CreditNoteMethod Method { get; private set; }

    /// <summary>Gets the ISO 4217 code every amount on the note is in: the invoice's currency.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Gets the tax percentage, copied from the invoice so the note reverses tax at the rate it was charged.</summary>
    public decimal TaxRate { get; private set; }

    /// <summary>Gets the sum of the line amounts before tax.</summary>
    public decimal SubTotal { get; private set; }

    /// <summary>Gets the tax on <see cref="SubTotal"/> at <see cref="TaxRate"/>, rounded to the currency's minor unit.</summary>
    public decimal Tax { get; private set; }

    /// <summary>Gets what the note credits in all: <see cref="SubTotal"/> plus <see cref="Tax"/>.</summary>
    public decimal Total { get; private set; }

    /// <summary>Gets why the note was issued.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Gets the gateway's reference of the refund a <see cref="CreditNoteMethod.GatewayRefund"/> note paid out; null otherwise.</summary>
    public string? RefundReference { get; private set; }

    /// <summary>Gets when the note was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets when the note got its number (UTC); null while it is a draft.</summary>
    public DateTimeOffset? IssuedAt { get; private set; }

    /// <summary>Gets when the note's value was given to the client (UTC); null until then.</summary>
    public DateTimeOffset? AppliedAt { get; private set; }

    /// <summary>Gets when the note was voided (UTC); null while it stands.</summary>
    public DateTimeOffset? VoidedAt { get; private set; }

    /// <summary>Gets why the note was voided; null while it stands.</summary>
    public string? VoidReason { get; private set; }

    /// <summary>Gets the read-only view of the note's lines.</summary>
    public IReadOnlyList<CreditNoteItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Gets whether the note's value counts against <see cref="Invoice.CreditNotedAmount"/>: an account credit note that has
    /// been issued and not voided. A gateway refund note is left out, because its refund is already on the invoice.
    /// </summary>
    public bool HoldsInvoiceCredit =>
        Method == CreditNoteMethod.AccountCredit && Status is (CreditNoteStatus.Issued or CreditNoteStatus.Applied);

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private CreditNote()
    {
    }

    /// <summary>
    /// Prepares a credit note for an invoice: copies its client, currency and tax rate, totals the lines and checks that the
    /// invoice can still be credited that much. Nothing is changed on the invoice yet.
    /// </summary>
    /// <param name="invoice">The paid invoice to credit.</param>
    /// <param name="items">The lines credited; at least one.</param>
    /// <param name="method">How the value goes back to the client.</param>
    /// <param name="reason">Why the note is issued; not blank.</param>
    /// <param name="createdAt">When the note is prepared (UTC).</param>
    /// <returns>The new <see cref="CreditNoteStatus.Draft"/> note.</returns>
    /// <exception cref="DomainRuleException">
    /// <c>CREDIT_NOTE_ITEMS_REQUIRED</c> without lines or a reason; <c>INVOICE_NOT_PAID</c>; <c>CREDIT_NOTE_TOP_UP_INVOICE</c>;
    /// <c>CREDIT_NOTE_EXCEEDS_CREDITABLE</c>; or <c>INVOICE_REFUND_EXCEEDS_PAID</c> when a gateway refund is more than is still
    /// refundable.
    /// </exception>
    public static CreditNote Create(
        Invoice invoice, IReadOnlyList<CreditNoteItem> items, CreditNoteMethod method, string reason, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0 || string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException(
                BillingErrors.CreditNoteItemsRequired, "A credit note needs at least one line and a reason.");
        }

        var note = new CreditNote
        {
            ClientId = invoice.ClientId,
            InvoiceId = invoice.Id,
            Status = CreditNoteStatus.Draft,
            Method = method,
            Currency = invoice.Currency,
            TaxRate = invoice.TaxRate,
            Reason = reason.Trim(),
            CreatedAt = createdAt,
        };
        note._items.AddRange(items);
        note.SubTotal = items.Sum(i => i.Amount);
        note.Tax = CurrencyCode.RoundToMinorUnits(note.SubTotal * note.TaxRate / 100, note.Currency);
        note.Total = note.SubTotal + note.Tax;

        invoice.EnsureCreditable(note.Total, createdAt);

        // A refund pays cash back, so it is also held to the cash the gateway can still return.
        if (method == CreditNoteMethod.GatewayRefund && note.Total > invoice.RefundableAmount)
        {
            throw new DomainRuleException(
                BillingErrors.InvoiceRefundExceedsPaid, $"Credit note {note.Total} is more than the {invoice.RefundableAmount} still refundable.");
        }

        return note;
    }

    /// <summary>Formats a sequence value as a credit note number: <c>CN-0001</c>, longer when the sequence outgrows four digits.</summary>
    /// <param name="sequenceValue">The value the number sequence handed out; positive.</param>
    /// <returns>The number.</returns>
    public static string FormatNumber(long sequenceValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceValue);

        return NumberPrefix + sequenceValue.ToString($"D{NumberDigits}", CultureInfo.InvariantCulture);
    }

    /// <summary>Gives the draft its number and makes it a standing document.</summary>
    /// <param name="number">The number handed out by the number sequence, such as <c>CN-0001</c>.</param>
    /// <param name="at">When the note is issued (UTC).</param>
    /// <exception cref="DomainRuleException"><c>CREDIT_NOTE_INVALID_STATE</c> when the note is not a draft.</exception>
    public void Issue(string number, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        if (Status != CreditNoteStatus.Draft)
        {
            throw new DomainRuleException(BillingErrors.CreditNoteInvalidState, $"Only a draft can be issued; current status is {Status}.");
        }

        Number = number;
        IssuedAt = at;
        Status = CreditNoteStatus.Issued;
    }

    /// <summary>
    /// Records that the note's value has been given to the client, and raises the event that mails them the note.
    /// </summary>
    /// <param name="at">When the value was given (UTC).</param>
    /// <param name="refundReference">The gateway's reference of the refund; required for a gateway refund note, ignored for account credit.</param>
    /// <exception cref="DomainRuleException"><c>CREDIT_NOTE_INVALID_STATE</c> when the note is not Issued, or a gateway refund note has no reference.</exception>
    public void MarkApplied(DateTimeOffset at, string? refundReference)
    {
        if (Status != CreditNoteStatus.Issued)
        {
            throw new DomainRuleException(BillingErrors.CreditNoteInvalidState, $"Only an issued note can be applied; current status is {Status}.");
        }

        if (Method == CreditNoteMethod.GatewayRefund && string.IsNullOrWhiteSpace(refundReference))
        {
            throw new DomainRuleException(BillingErrors.CreditNoteInvalidState, "A gateway refund note records the refund it paid out.");
        }

        RefundReference = Method == CreditNoteMethod.GatewayRefund ? refundReference : null;
        AppliedAt = at;
        Status = CreditNoteStatus.Applied;

        // A factory, so the event names the id the insert assigned, not the 0 it has before the save.
        AddDomainEvent(() => new CreditNoteIssuedEvent(Id, ClientId, Number!, InvoiceId, Total, Currency) { OccurredAt = at });
    }

    /// <summary>
    /// Voids the note. It keeps its number and stays on record. A note paid out as a gateway refund cannot be voided, because
    /// the money has already left.
    /// </summary>
    /// <param name="reason">Why the note is voided; not blank.</param>
    /// <param name="at">When it is voided (UTC).</param>
    /// <exception cref="DomainRuleException">
    /// <c>CREDIT_NOTE_ALREADY_VOIDED</c>, <c>CREDIT_NOTE_INVALID_STATE</c> for a draft, <c>CREDIT_NOTE_REFUND_NOT_VOIDABLE</c> for a
    /// gateway refund note that was paid out.
    /// </exception>
    public void Void(string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (Status == CreditNoteStatus.Voided)
        {
            throw new DomainRuleException(BillingErrors.CreditNoteAlreadyVoided, "This credit note is already voided.");
        }

        if (Status == CreditNoteStatus.Draft)
        {
            throw new DomainRuleException(BillingErrors.CreditNoteInvalidState, "A draft was never issued, so there is nothing to void.");
        }

        if (Method == CreditNoteMethod.GatewayRefund && Status == CreditNoteStatus.Applied)
        {
            throw new DomainRuleException(
                BillingErrors.CreditNoteRefundNotVoidable, "The refund this note paid out cannot be taken back, so the note cannot be voided.");
        }

        VoidReason = reason.Trim();
        VoidedAt = at;
        Status = CreditNoteStatus.Voided;
    }
}
