namespace Innovayse.Application.Clients.DTOs;

using Innovayse.Domain.Clients;

/// <summary>Full details DTO for a client, including contacts and billing address.</summary>
/// <param name="Id">Client primary key.</param>
/// <param name="UserId">Linked Identity user ID.</param>
/// <param name="Email">Email address from the linked Identity user.</param>
/// <param name="FirstName">Client first name.</param>
/// <param name="LastName">Client last name.</param>
/// <param name="CompanyName">Company name (null for individuals).</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Status">Current account status.</param>
/// <param name="Street">Billing street address.</param>
/// <param name="Address2">Second address line.</param>
/// <param name="City">Billing city.</param>
/// <param name="State">Billing state or region.</param>
/// <param name="PostCode">Billing postcode.</param>
/// <param name="Country">Billing country code.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="PaymentMethod">Preferred payment method.</param>
/// <param name="BillingContact">Billing contact reference.</param>
/// <param name="AdminNotes">Internal admin notes.</param>
/// <param name="NotifyGeneral">General account emails enabled.</param>
/// <param name="NotifyInvoice">Invoice emails enabled.</param>
/// <param name="NotifySupport">Support emails enabled.</param>
/// <param name="NotifyProduct">Product emails enabled.</param>
/// <param name="NotifyDomain">Domain emails enabled.</param>
/// <param name="NotifyAffiliate">Affiliate emails enabled.</param>
/// <param name="LateFees">Late fees applied.</param>
/// <param name="OverdueNotices">Overdue notices sent.</param>
/// <param name="TaxExempt">Tax exempt flag.</param>
/// <param name="SeparateInvoices">Separate invoices per service.</param>
/// <param name="DisableCcProcessing">CC auto-processing disabled.</param>
/// <param name="MarketingOptIn">Marketing emails opt-in.</param>
/// <param name="StatusUpdate">Status update emails sent.</param>
/// <param name="AllowSso">Single sign-on allowed.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="Contacts">Additional contacts linked to this client.</param>
public record ClientDto(
    int Id,
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
    ClientStatus Status,
    string? Street,
    string? Address2,
    string? City,
    string? State,
    string? PostCode,
    string? Country,
    string? Currency,
    string? PaymentMethod,
    string? BillingContact,
    string? AdminNotes,
    bool NotifyGeneral,
    bool NotifyInvoice,
    bool NotifySupport,
    bool NotifyProduct,
    bool NotifyDomain,
    bool NotifyAffiliate,
    bool LateFees,
    bool OverdueNotices,
    bool TaxExempt,
    bool SeparateInvoices,
    bool DisableCcProcessing,
    bool MarketingOptIn,
    bool StatusUpdate,
    bool AllowSso,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ContactDto> Contacts);
