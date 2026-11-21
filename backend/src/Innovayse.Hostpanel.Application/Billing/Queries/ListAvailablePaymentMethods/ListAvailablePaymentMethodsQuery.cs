namespace Innovayse.Hostpanel.Application.Billing.Queries.ListAvailablePaymentMethods;

/// <summary>
/// Query for the payment methods a payer can pick at checkout right now.
/// </summary>
/// <remarks>
/// Availability is a property of the deployment, the admin's integration switches, the
/// currency the payer is billed in and the product groups being paid for. By default that currency is the
/// caller's own. The order flow names it instead, because it checks a method for a guest whose client, and so whose
/// currency, does not exist yet at the moment the caller is asked about. A group may restrict the methods it is paid
/// with, so the cart's products (or the invoice) narrow the list to what every one of their groups allows.
/// </remarks>
/// <param name="CurrencyCode">
/// ISO 4217 alpha code to list for, any case; <see langword="null"/> to list for the caller's
/// own currency. A code the panel does not offer lists nothing.
/// </param>
/// <param name="ProductIds">
/// The products of the cart being paid for; <see langword="null"/> or empty puts no group restriction on the list.
/// </param>
/// <param name="InvoiceId">
/// An invoice of the caller's own being paid, whose services' product groups restrict the list the same way;
/// <see langword="null"/> for none. An invoice that is not the caller's is answered as <c>INVOICE_NOT_FOUND</c>.
/// </param>
public sealed record ListAvailablePaymentMethodsQuery(
    string? CurrencyCode = null,
    IReadOnlyList<int>? ProductIds = null,
    int? InvoiceId = null);
