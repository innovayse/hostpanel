namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>The account switches staff set on a client: billing behaviour, marketing consent and sign-in options.</summary>
/// <param name="OverdueNotices">Payment reminder emails for unpaid invoices are sent.</param>
/// <param name="TaxExempt">The client is exempt from tax: no tax is added to their invoices and no tax rate can be set on them.</param>
/// <param name="TaxId">The client's tax identification number (for example a VAT number), or null when none is recorded.</param>
/// <param name="TaxExemptionReason">Why the client is tax exempt, or null when no reason is recorded.</param>
/// <param name="SeparateInvoices">Renewals are invoiced one invoice per line instead of one per run.</param>
/// <param name="DisableCcProcessing">Card payments and saved card charges are refused for the client.</param>
/// <param name="MarketingOptIn">The client has opted in to marketing emails.</param>
/// <param name="StatusUpdate">Status update emails are sent.</param>
/// <param name="AllowSso">The client may sign in to the hosting panels of their services through single sign-on.</param>
public sealed record ClientSettingsDto(
    bool OverdueNotices,
    bool TaxExempt,
    string? TaxId,
    string? TaxExemptionReason,
    bool SeparateInvoices,
    bool DisableCcProcessing,
    bool MarketingOptIn,
    bool StatusUpdate,
    bool AllowSso);
