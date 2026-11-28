using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The invoice that bills one period of a service, as another module reads it (whether the conversion
/// invoice of a trial is there and paid) without reading the Billing tables itself.
/// </summary>
/// <param name="InvoiceId">The invoice id.</param>
/// <param name="Status">The invoice's current status.</param>
public sealed record ServiceRenewalInvoiceDto(int InvoiceId, InvoiceStatus Status)
{
    /// <summary>
    /// Whether the invoice is paid (one of <see cref="Invoice.PaidStatuses"/>, so also partly refunded since) or its
    /// payment is pending: the payment completes the period, so the service it bills is kept.
    /// </summary>
    public bool IsPaidOrPaymentPending => Invoice.PaidStatuses.Contains(Status) || Status == InvoiceStatus.PaymentPending;
}
