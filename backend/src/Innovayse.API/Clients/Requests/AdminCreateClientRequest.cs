namespace Innovayse.API.Clients.Requests;

/// <summary>
/// HTTP request body for POST /api/clients/admin.
/// Contains all fields needed to create a client account from the admin panel.
/// </summary>
/// <param name="CreateNewUser">True to create a new Identity user; false to link an existing one.</param>
/// <param name="ExistingUserId">The Identity user ID to link when <paramref name="CreateNewUser"/> is false.</param>
/// <param name="Email">Email address for the new user (required when <paramref name="CreateNewUser"/> is true).</param>
/// <param name="Password">Password for the new user (required when <paramref name="CreateNewUser"/> is true).</param>
/// <param name="FirstName">Client's first name.</param>
/// <param name="LastName">Client's last name.</param>
/// <param name="CompanyName">Optional company name.</param>
/// <param name="Phone">Optional phone number.</param>
/// <param name="Street">Billing address street line.</param>
/// <param name="Address2">Billing address second line.</param>
/// <param name="City">Billing address city.</param>
/// <param name="State">Billing address state or region.</param>
/// <param name="PostCode">Billing address postal code.</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Language">Preferred language code (e.g. "en", "hy", "ru").</param>
/// <param name="PaymentMethod">Preferred payment method label.</param>
/// <param name="BillingContact">Billing contact reference.</param>
/// <param name="AdminNotes">Internal admin-only notes.</param>
/// <param name="Status">Account status string (Active, Inactive, Suspended, Closed). Defaults to Active.</param>
/// <param name="NotifyGeneral">Enable general account notification emails.</param>
/// <param name="NotifyInvoice">Enable invoice and billing notification emails.</param>
/// <param name="NotifySupport">Enable support ticket notification emails.</param>
/// <param name="NotifyProduct">Enable product lifecycle notification emails.</param>
/// <param name="NotifyDomain">Enable domain registration notification emails.</param>
/// <param name="NotifyAffiliate">Enable affiliate program notification emails.</param>
/// <param name="LateFees">Apply late fees to this client's overdue invoices.</param>
/// <param name="OverdueNotices">Send overdue payment notices to this client.</param>
/// <param name="TaxExempt">Exempt this client from tax calculations.</param>
/// <param name="SeparateInvoices">Generate separate invoices per service.</param>
/// <param name="DisableCcProcessing">Disable automatic credit card processing.</param>
/// <param name="MarketingOptIn">Client has opted in to marketing emails.</param>
/// <param name="StatusUpdate">Send status update notification emails.</param>
/// <param name="AllowSso">Allow single sign-on for this client.</param>
public record AdminCreateClientRequest(
    bool CreateNewUser,
    string? ExistingUserId,
    string? Email,
    string? Password,
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
    string? Language,
    string? PaymentMethod,
    string? BillingContact,
    string? AdminNotes,
    string? Status,
    // Notification preferences
    bool NotifyGeneral = true,
    bool NotifyInvoice = true,
    bool NotifySupport = true,
    bool NotifyProduct = true,
    bool NotifyDomain = true,
    bool NotifyAffiliate = true,
    // Billing settings
    bool LateFees = true,
    bool OverdueNotices = true,
    bool TaxExempt = false,
    bool SeparateInvoices = false,
    bool DisableCcProcessing = false,
    bool MarketingOptIn = false,
    bool StatusUpdate = true,
    bool AllowSso = true);
