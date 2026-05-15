namespace Innovayse.API.Clients.Requests;

/// <summary>HTTP request body for PUT /api/clients/{id} and PUT /api/me.</summary>
/// <param name="Email">Updated email address (null to keep current).</param>
/// <param name="FirstName">Updated first name.</param>
/// <param name="LastName">Updated last name.</param>
/// <param name="CompanyName">Updated company name (null to clear).</param>
/// <param name="Phone">Updated phone (null to clear).</param>
/// <param name="Street">Updated billing street.</param>
/// <param name="Address2">Updated second address line.</param>
/// <param name="City">Updated billing city.</param>
/// <param name="State">Updated billing state.</param>
/// <param name="PostCode">Updated billing postcode.</param>
/// <param name="Country">Updated billing country code (2 chars).</param>
/// <param name="Currency">ISO 4217 currency code (null to keep current).</param>
/// <param name="PaymentMethod">Payment method label (null to keep current).</param>
/// <param name="BillingContact">Billing contact reference (null to keep current).</param>
/// <param name="AdminNotes">Internal admin notes (null to keep current).</param>
/// <param name="NotifyGeneral">General email notifications.</param>
/// <param name="NotifyInvoice">Invoice email notifications.</param>
/// <param name="NotifySupport">Support email notifications.</param>
/// <param name="NotifyProduct">Product email notifications.</param>
/// <param name="NotifyDomain">Domain email notifications.</param>
/// <param name="NotifyAffiliate">Affiliate email notifications.</param>
/// <param name="LateFees">Apply late fees.</param>
/// <param name="OverdueNotices">Send overdue notices.</param>
/// <param name="TaxExempt">Tax exempt flag.</param>
/// <param name="SeparateInvoices">Separate invoices per service.</param>
/// <param name="DisableCcProcessing">Disable CC auto-charge.</param>
/// <param name="MarketingOptIn">Marketing emails opt-in.</param>
/// <param name="StatusUpdate">Send status update emails.</param>
/// <param name="AllowSso">Allow single sign-on.</param>
/// <param name="Status">Client status (Active, Inactive, Suspended, Closed). Null to keep current.</param>
public record UpdateClientRequest(
    string? Email,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Phone,
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
    string? Status);
