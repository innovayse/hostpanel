using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Application.Reports.Common;

/// <summary>One row in the Services report.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="ClientId">The client identifier.</param>
/// <param name="ClientName">The client name.</param>
/// <param name="ProductId">The product identifier.</param>
/// <param name="ProductName">The product name.</param>
/// <param name="Domain">The domain.</param>
/// <param name="BillingCycle">The billing cycle.</param>
/// <param name="FirstPaymentAmount">The first payment amount.</param>
/// <param name="RecurringAmount">The recurring amount.</param>
/// <param name="CurrencyCode">ISO 4217 code of the currency both amounts are in: the owning client's billing currency.</param>
/// <param name="PaymentMethod">The payment method.</param>
/// <param name="RegistrationDate">The registration date.</param>
/// <param name="NextDueDate">The next due date.</param>
/// <param name="TerminatedAt">The terminated at.</param>
/// <param name="Status">The status.</param>
/// <param name="Notes">The notes.</param>
public record ServiceReportDto(
    int Id,
    int ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    string? Domain,
    BillingCycle BillingCycle,
    decimal FirstPaymentAmount,
    decimal RecurringAmount,
    string CurrencyCode,
    string? PaymentMethod,
    string RegistrationDate,
    string? NextDueDate,
    string? TerminatedAt,
    ServiceStatus Status,
    string? Notes);
