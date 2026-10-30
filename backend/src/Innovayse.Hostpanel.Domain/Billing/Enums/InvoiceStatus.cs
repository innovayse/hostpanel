namespace Innovayse.Hostpanel.Domain.Billing.Enums;

/// <summary>Lifecycle states for an <see cref="Invoice"/>.</summary>
public enum InvoiceStatus
{
    /// <summary>Invoice is saved but not yet sent to the client.</summary>
    Draft = 0,

    /// <summary>Invoice has been issued and payment is expected.</summary>
    Unpaid = 1,

    /// <summary>Invoice has been paid in full.</summary>
    Paid = 2,

    /// <summary>Invoice due date has passed without payment.</summary>
    Overdue = 3,

    /// <summary>Invoice has been voided and will not be collected.</summary>
    Cancelled = 4,

    /// <summary>Payment has been refunded to the client.</summary>
    Refunded = 5,

    /// <summary>Invoice has been sent to a collections agency.</summary>
    Collections = 6,

    /// <summary>Payment has been initiated and is awaiting confirmation.</summary>
    PaymentPending = 7,

    /// <summary>
    /// Paid, and part of the money has been refunded; the rest is still held and can still be
    /// refunded. A refund that reaches the whole amount paid turns it <see cref="Refunded"/>.
    /// </summary>
    PartiallyRefunded = 8,
}
