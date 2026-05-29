namespace Innovayse.Domain.Billing;

/// <summary>Classifies financial transactions recorded against an <see cref="Invoice"/>.</summary>
public enum InvoiceTransactionType
{
    /// <summary>A payment made towards the invoice balance.</summary>
    Payment,

    /// <summary>A refund returned to the client.</summary>
    Refund,

    /// <summary>A credit applied to reduce the invoice balance.</summary>
    Credit,
}
