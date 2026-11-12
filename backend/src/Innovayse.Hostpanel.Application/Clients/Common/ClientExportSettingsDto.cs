namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>The account switches an admin sets on a client, as the <c>profileData</c> section of a client data export lists them.</summary>
/// <param name="OverdueNotices">Whether payment reminder emails for unpaid invoices are sent.</param>
/// <param name="TaxExempt">Whether the client is exempt from tax.</param>
/// <param name="TaxId">The client's tax identification number, or null when none is recorded.</param>
/// <param name="TaxExemptionReason">Why the client is tax exempt, or null when no reason is recorded.</param>
/// <param name="SeparateInvoices">Whether renewals are invoiced one invoice per line.</param>
/// <param name="DisableCcProcessing">Whether card payments and saved card charges are refused.</param>
/// <param name="StatusUpdate">Whether status update emails are sent.</param>
/// <param name="AllowSso">Whether single sign-on into the hosting panels of the client's services is allowed.</param>
public sealed record ClientExportSettingsDto(
    bool OverdueNotices,
    bool TaxExempt,
    string? TaxId,
    string? TaxExemptionReason,
    bool SeparateInvoices,
    bool DisableCcProcessing,
    bool StatusUpdate,
    bool AllowSso);
