namespace Innovayse.Domain.Billing;

/// <summary>Lifecycle states for an <see cref="Invoice"/>.</summary>
public enum InvoiceStatus
{
<<<<<<< HEAD
    /// <summary>Invoice is saved but not yet sent to the client.</summary>
=======
    /// <summary>Invoice has been created but not yet published to the client.</summary>
>>>>>>> origin/main
    Draft,

    /// <summary>Invoice has been issued and payment is expected.</summary>
    Unpaid,

    /// <summary>Invoice has been paid in full.</summary>
    Paid,

    /// <summary>Invoice due date has passed without payment.</summary>
    Overdue,

    /// <summary>Invoice has been voided and will not be collected.</summary>
    Cancelled,

<<<<<<< HEAD
    /// <summary>Payment has been refunded to the client.</summary>
    Refunded,

    /// <summary>Invoice has been sent to a collections agency.</summary>
    Collections,

    /// <summary>Payment has been initiated and is awaiting confirmation.</summary>
    PaymentPending,
=======
    /// <summary>Invoice payment has been refunded to the client.</summary>
    Refunded,
>>>>>>> origin/main
}
