namespace Innovayse.Domain.Billing;

/// <summary>Lifecycle states for an <see cref="Invoice"/>.</summary>
public enum InvoiceStatus
{
    /// <summary>Invoice has been issued and payment is expected.</summary>
    Unpaid,

    /// <summary>Invoice has been paid in full.</summary>
    Paid,

    /// <summary>Invoice due date has passed without payment.</summary>
    Overdue,

    /// <summary>Invoice has been voided and will not be collected.</summary>
    Cancelled,
}
