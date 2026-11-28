namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// Refusal codes the Billing aggregates (<see cref="Currency"/>, <see cref="Invoice"/>,
/// <see cref="Quote"/>, <see cref="FulfilmentRecord"/>) throw as <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so <c>ResourceCoverageTests</c> (which only reflects over
/// <c>HostpanelErrorCodes</c>) still requires a sentence for each one in every language.
/// </remarks>
public static class BillingErrors
{
    /// <summary>The base currency cannot be disabled (400): <see cref="Currency.Disable"/>.</summary>
    public const string CurrencyBaseDisableNotAllowed = "CURRENCY_BASE_DISABLE_NOT_ALLOWED";

    /// <summary>The rate of a tax rule is outside 0 to 100 percent (400): <see cref="TaxRule.Create"/> and <see cref="TaxRule.Update"/>.</summary>
    public const string TaxRuleRateInvalid = "TAX_RULE_RATE_INVALID";

    /// <summary>The rate of a VAT rate row is outside 0 to 100 percent (400): <see cref="VatRate.Create"/> and <see cref="VatRate.Update"/>.</summary>
    public const string VatRateInvalid = "VAT_RATE_INVALID";

    /// <summary>Only a Draft invoice may be published (400): <see cref="Invoice.Publish"/>.</summary>
    public const string InvoiceNotDraft = "INVOICE_NOT_DRAFT";

    /// <summary>
    /// The invoice is no longer editable (400): <see cref="Invoice.AddItem"/> once it has left
    /// Draft/Unpaid.
    /// </summary>
    public const string InvoiceNotEditable = "INVOICE_NOT_EDITABLE";

    /// <summary>
    /// The invoice is not open for payment, because it is not Unpaid or Overdue (400): <see cref="Invoice.MarkPaid"/>,
    /// <see cref="Invoice.Settle"/>, <see cref="Invoice.AddPayment"/> and <see cref="Invoice.RequestBankTransfer"/>.
    /// </summary>
    public const string InvoiceNotPayable = "INVOICE_NOT_PAYABLE";

    /// <summary>
    /// The invoice is open but owes nothing, so there is nothing to transfer or charge (400):
    /// <see cref="Invoice.RequestBankTransfer"/>.
    /// </summary>
    public const string InvoiceNothingDue = "INVOICE_NOTHING_DUE";

    /// <summary>
    /// The id names no line item on this invoice (404): <see cref="Invoice.RemoveItem"/> and
    /// <see cref="Invoice.UpdateItem"/>.
    /// </summary>
    public const string InvoiceItemNotFound = "INVOICE_ITEM_NOT_FOUND";

    /// <summary>
    /// The invoice is not eligible to be marked overdue (400): <see cref="Invoice.MarkOverdue"/>
    /// requires <c>Unpaid</c>.
    /// </summary>
    public const string InvoiceNotUnpaid = "INVOICE_NOT_UNPAID";

    /// <summary>
    /// No manual bank transfer is waiting on the invoice (409): <see cref="Invoice.ConfirmBankTransfer"/> and
    /// <see cref="Invoice.RejectBankTransfer"/>. A second confirm of the same transfer answers with it too.
    /// </summary>
    public const string InvoiceBankTransferNotPending = "INVOICE_BANK_TRANSFER_NOT_PENDING";

    /// <summary>
    /// The invoice is not eligible to be marked lapsed (400): <see cref="Invoice.MarkLapsed"/>
    /// requires <c>Overdue</c>.
    /// </summary>
    public const string InvoiceNotOverdue = "INVOICE_NOT_OVERDUE";

    /// <summary>
    /// The fulfilment record has not failed, so there is nothing to retry (409):
    /// <see cref="FulfilmentRecord.Retry"/>.
    /// </summary>
    public const string FulfilmentRecordNotRetryable = "FULFILMENT_RECORD_NOT_RETRYABLE";

    /// <summary>
    /// The dispatcher never reported an outcome before its claim ran out, because the process stopped or lost
    /// the database between claiming the record and saving the result: <see cref="FulfilmentRecord.Claim"/>.
    /// Stored in <see cref="FulfilmentRecord.LastErrorCode"/>, never answered.
    /// </summary>
    public const string FulfilmentAttemptAbandoned = "FULFILMENT_ATTEMPT_ABANDONED";

    /// <summary>
    /// The fulfilment plugin did not answer within the call timeout. Stored in
    /// <see cref="FulfilmentRecord.LastErrorCode"/>, never answered.
    /// </summary>
    public const string FulfilmentPluginTimedOut = "FULFILMENT_PLUGIN_TIMED_OUT";

    /// <summary>
    /// The fulfilment plugin failed the delivery. Its own error text is only logged. Stored in
    /// <see cref="FulfilmentRecord.LastErrorCode"/>, never answered.
    /// </summary>
    public const string FulfilmentPluginFailed = "FULFILMENT_PLUGIN_FAILED";

    /// <summary>
    /// The line is not a domain renewal left claimed, so there is no claim to release (409):
    /// <see cref="Invoice.ReleaseRenewalClaim"/>.
    /// </summary>
    public const string RenewalClaimNotHeld = "RENEWAL_CLAIM_NOT_HELD";

    /// <summary>
    /// The invoice is past the point where it can be cancelled (400): it is paid, refunded or
    /// already cancelled; <see cref="Invoice.Cancel"/>.
    /// </summary>
    public const string InvoiceCancelNotAllowed = "INVOICE_CANCEL_NOT_ALLOWED";

    /// <summary>
    /// The invoice must be Paid for this action (400): <see cref="Invoice.MarkUnpaid"/>; or Paid or
    /// PartiallyRefunded for a refund: <see cref="Invoice.Refund"/>, <see cref="Invoice.AddRefund"/>
    /// and <see cref="Invoice.BeginRefund"/>.
    /// </summary>
    public const string InvoiceNotPaid = "INVOICE_NOT_PAID";

    /// <summary>
    /// The refund is larger than what was paid and not yet refunded (400):
    /// <see cref="Invoice.AddRefund"/>.
    /// </summary>
    public const string InvoiceRefundExceedsPaid = "INVOICE_REFUND_EXCEEDS_PAID";

    /// <summary>
    /// Another refund of the invoice is still waiting for its gateway (409):
    /// <see cref="Invoice.BeginRefund"/> and <see cref="Invoice.AddRefund"/>.
    /// </summary>
    public const string InvoiceRefundConflict = "INVOICE_REFUND_CONFLICT";

    /// <summary>
    /// The credit is larger than what the invoice still owes (400): <see cref="Invoice.ApplyCredit"/>.
    /// </summary>
    public const string InvoiceCreditExceedsDue = "INVOICE_CREDIT_EXCEEDS_DUE";

    /// <summary>
    /// The payment is larger than what the invoice still owes (400): <see cref="Invoice.AddPayment"/>.
    /// </summary>
    public const string InvoicePaymentExceedsDue = "INVOICE_PAYMENT_EXCEEDS_DUE";

    /// <summary>
    /// The invoice still owes money, so it cannot simply be marked paid; it is settled instead (400):
    /// <see cref="Invoice.MarkPaid"/>.
    /// </summary>
    public const string InvoiceAmountStillDue = "INVOICE_AMOUNT_STILL_DUE";

    /// <summary>
    /// The invoice already took a payment, so cancelling it, reopening it or closing it as history
    /// would strand that money (400); the payment is deleted or refunded first. See
    /// <see cref="Invoice.EnsureCancellable"/>, <see cref="Invoice.MarkUnpaid"/> and <see cref="Invoice.RecordPaidHistory"/>.
    /// </summary>
    public const string InvoiceHasPayments = "INVOICE_HAS_PAYMENTS";

    /// <summary>
    /// A line or tax change would make the invoice charge less than the payments it already took
    /// (400). See <see cref="Invoice.RemoveItem"/>, <see cref="Invoice.UpdateItem"/> and <see cref="Invoice.UpdateOptions"/>.
    /// </summary>
    public const string InvoiceTotalBelowPaid = "INVOICE_TOTAL_BELOW_PAID";

    /// <summary>
    /// The quote is no longer editable (400): <see cref="Quote.AddItem"/> once its stage has left
    /// Draft/Delivered/OnHold.
    /// </summary>
    public const string QuoteNotEditable = "QUOTE_NOT_EDITABLE";

    /// <summary>The quote's stage does not allow acceptance (400): <see cref="Quote.Accept"/>.</summary>
    public const string QuoteNotAcceptable = "QUOTE_NOT_ACCEPTABLE";

    /// <summary>
    /// The id names no line item on this quote (404): <see cref="Quote.RemoveItem"/> and
    /// <see cref="Quote.UpdateItem"/>.
    /// </summary>
    public const string QuoteItemNotFound = "QUOTE_ITEM_NOT_FOUND";

    /// <summary>
    /// A transaction must move money in at least one direction (422): <see cref="Transaction.Create"/>
    /// and <see cref="Transaction.CorrectAmounts"/>.
    /// </summary>
    public const string TransactionAmountRequired = "TRANSACTION_AMOUNT_REQUIRED";

    /// <summary>
    /// The entry is part of the client's credit history and cannot be edited or deleted (409): <see cref="Transaction.EnsureEditable"/>.
    /// A mistake is put right with a new credit adjustment.
    /// </summary>
    public const string TransactionCreditLocked = "TRANSACTION_CREDIT_LOCKED";

    /// <summary>
    /// A credit top-up invoice is one fixed line and cannot be edited, taxed or discounted (400): <see cref="Invoice.AddItem"/>
    /// and every other change to its lines or tax rate.
    /// </summary>
    public const string InvoiceTopUpFixed = "INVOICE_TOP_UP_FIXED";

    /// <summary>Credit cannot be applied to a credit top-up invoice, which would pay for credit with credit (400): <see cref="Invoice.ApplyCredit"/>.</summary>
    public const string InvoiceTopUpNoCredit = "INVOICE_TOP_UP_NO_CREDIT";

    /// <summary>
    /// A paid credit top-up is refunded by staff by hand only (400): not through the gateway and not back to the balance,
    /// which would be the same money again.
    /// </summary>
    public const string InvoiceTopUpRefundManualOnly = "INVOICE_TOP_UP_REFUND_MANUAL_ONLY";

    /// <summary>The id names no credit note, or none the caller may see (404).</summary>
    public const string CreditNoteNotFound = "CREDIT_NOTE_NOT_FOUND";

    /// <summary>A credit note cannot be issued on a credit top-up invoice, which is credit already (400): <see cref="Invoice.EnsureCreditable"/>.</summary>
    public const string CreditNoteTopUpInvoice = "CREDIT_NOTE_TOP_UP_INVOICE";

    /// <summary>
    /// The credit note is more than the invoice can still be credited once every earlier credit note and refund is counted
    /// (400): <see cref="Invoice.EnsureCreditable"/>.
    /// </summary>
    public const string CreditNoteExceedsCreditable = "CREDIT_NOTE_EXCEEDS_CREDITABLE";

    /// <summary>A credit note needs at least one line, and every line a description, a price and a quantity (400): <see cref="CreditNote.Create"/>.</summary>
    public const string CreditNoteItemsRequired = "CREDIT_NOTE_ITEMS_REQUIRED";

    /// <summary>Only a draft credit note can be issued, and this one already has a number (409): <see cref="CreditNote.Issue"/>.</summary>
    public const string CreditNoteInvalidState = "CREDIT_NOTE_INVALID_STATE";

    /// <summary>A credit note is applied only once it is issued, and this one is a draft, already applied or voided (409): <see cref="CreditNote.MarkApplied"/>.</summary>
    public const string CreditNoteNotIssued = "CREDIT_NOTE_NOT_ISSUED";

    /// <summary>A gateway refund credit note is applied with the gateway's reference of the refund it paid out, and none was given (409): <see cref="CreditNote.MarkApplied"/>.</summary>
    public const string CreditNoteRefundReferenceRequired = "CREDIT_NOTE_REFUND_REFERENCE_REQUIRED";

    /// <summary>A draft credit note was never issued, so there is nothing to void (409): <see cref="CreditNote.Void"/>.</summary>
    public const string CreditNoteDraftNotVoidable = "CREDIT_NOTE_DRAFT_NOT_VOIDABLE";

    /// <summary>More credit note value is released from the invoice than its credit notes ever noted on it (409): <see cref="Invoice.RemoveCreditNoted"/>.</summary>
    public const string InvoiceCreditNotedExceeded = "INVOICE_CREDIT_NOTED_EXCEEDED";

    /// <summary>The credit note was already voided (409): <see cref="CreditNote.Void"/>.</summary>
    public const string CreditNoteAlreadyVoided = "CREDIT_NOTE_ALREADY_VOIDED";

    /// <summary>
    /// A credit note paid out as a gateway refund cannot be voided, because the money has left and no void can bring it back
    /// (409): <see cref="CreditNote.Void"/>.
    /// </summary>
    public const string CreditNoteRefundNotVoidable = "CREDIT_NOTE_REFUND_NOT_VOIDABLE";
}
