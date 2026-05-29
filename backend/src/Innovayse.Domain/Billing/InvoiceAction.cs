namespace Innovayse.Domain.Billing;

/// <summary>Determines how and when a billable item should be invoiced.</summary>
public enum InvoiceAction
{
    /// <summary>Item stays uninvoiced until manually selected.</summary>
    DontInvoice,

    /// <summary>Item will be invoiced on the next scheduled cron run.</summary>
    InvoiceOnNextCron,

    /// <summary>Item will be added to the client's next invoice.</summary>
    AddToNextInvoice,

    /// <summary>Item will be invoiced when its due date arrives.</summary>
    InvoiceForDueDate,

    /// <summary>Item recurs at a set interval and is automatically invoiced each cycle.</summary>
    Recur,
}
