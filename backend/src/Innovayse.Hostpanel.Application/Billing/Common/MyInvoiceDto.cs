using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The client's own invoice as the client area reads it. The gateway reference, the staff
/// notes and the owner fields of <see cref="InvoiceDto"/> are not part of it, so they cannot
/// reach the client by any path.
/// </summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="Status">Current invoice status.</param>
/// <param name="Currency">ISO 4217 code the invoice is payable in.</param>
/// <param name="InvoiceDate">Issue date.</param>
/// <param name="DueDate">Payment due date.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="PaidAt">Payment timestamp; null while never paid, kept after a refund.</param>
/// <param name="Total">Final total after tax and credit.</param>
/// <param name="SubTotal">Sum of the line amounts before tax and credit.</param>
/// <param name="Tax">Computed tax amount.</param>
/// <param name="TaxRate">Tax rate percentage.</param>
/// <param name="Credit">Total credit applied.</param>
/// <param name="AmountDue">What is still owed: the total less the payments recorded against it; zero once the invoice is closed. The pay button and payment page charge this.</param>
/// <param name="IsCreditTopUp">Whether this is a credit top-up invoice: credit cannot be applied to it, and paying it puts its amount on the balance.</param>
/// <param name="IsBankTransferPending">Whether the client chose a manual bank transfer that staff have not yet confirmed or rejected. A <c>PaymentPending</c> invoice that is not (an imported one) is false, so the portal shows bank details only for a real transfer.</param>
/// <param name="PaymentMethod">Preferred payment method; null when not specified.</param>
/// <param name="Items">Line items on the invoice.</param>
/// <param name="Transactions">Money movements recorded against the invoice.</param>
public sealed record MyInvoiceDto(
    int Id,
    InvoiceStatus Status,
    string Currency,
    DateTimeOffset InvoiceDate,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    decimal Total,
    decimal SubTotal,
    decimal Tax,
    decimal TaxRate,
    decimal Credit,
    decimal AmountDue,
    bool IsCreditTopUp,
    bool IsBankTransferPending,
    string? PaymentMethod,
    IReadOnlyList<InvoiceItemDto> Items,
    IReadOnlyList<MyInvoiceTransactionDto> Transactions);
