using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;
/// <summary>DTO representing a full invoice with its line items and transactions.</summary>
/// <param name="Id">Invoice primary key.</param>
/// <param name="ClientId">FK to the owning client.</param>
/// <param name="ClientName">Display name of the owning client; every invoice has one, since its client cannot be deleted.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="Currency">ISO 4217 currency code the invoice was raised and is payable in.</param>
/// <param name="Dates">Issue, due, creation and payment dates.</param>
/// <param name="Amounts">Sub total, credit, tax, tax rate and total.</param>
/// <param name="AmountDue">What is still owed: the total less the payments recorded against it; zero once the invoice is closed.</param>
/// <param name="CreditableAmount">What a credit note may still cover: zero unless the invoice is paid, and zero for a credit top-up.</param>
/// <param name="IsCreditTopUp">Whether this is a credit top-up invoice: one fixed line that puts its amount on the client's balance when paid.</param>
/// <param name="Payment">Payment method and gateway reference.</param>
/// <param name="Notes">Optional invoice notes.</param>
/// <param name="Items">Line items on the invoice.</param>
/// <param name="Transactions">Financial transactions recorded against the invoice.</param>
public sealed record InvoiceDto(
    int Id,
    int ClientId,
    string ClientName,
    InvoiceStatus Status,
    string Currency,
    InvoiceDatesDto Dates,
    InvoiceAmountsDto Amounts,
    decimal AmountDue,
    decimal CreditableAmount,
    bool IsCreditTopUp,
    InvoicePaymentDto Payment,
    string? Notes,
    IReadOnlyList<InvoiceItemDto> Items,
    IReadOnlyList<InvoiceTransactionDto> Transactions);
