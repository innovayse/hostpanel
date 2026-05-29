namespace Innovayse.Domain.Billing;

/// <summary>Lifecycle states for an <see cref="Invoice"/>.</summary>
public enum InvoiceStatus
{
    /// <summary>Invoice is saved but not yet sent to the client.</summary>
    Draft,

    /// <summary>Invoice has been issued and payment is expected.</summary>
    Unpaid,

    /// <summary>Invoice has been paid in full.</summary>
    Paid,

    /// <summary>Invoice due date has passed without payment.</summary>
    Overdue,

    /// <summary>Invoice has been voided and will not be collected.</summary>
    Cancelled,

    /// <summary>Payment has been refunded to the client.</summary>
    Refunded,

    /// <summary>Invoice has been sent to a collections agency.</summary>
    Collections,

    /// <summary>Payment has been initiated and is awaiting confirmation.</summary>
    PaymentPending,
}
