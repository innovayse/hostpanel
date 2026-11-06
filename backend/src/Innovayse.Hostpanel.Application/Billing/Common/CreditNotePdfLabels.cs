namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>Every word a credit note PDF prints, in one language.</summary>
/// <param name="Document">The labels shared with every document: the table, the totals and the footer.</param>
/// <param name="Title">The name of the document, printed before its number.</param>
/// <param name="IssueDate">Label of the issue date.</param>
/// <param name="Invoice">Label of the credited invoice.</param>
/// <param name="Status">Label of the status.</param>
/// <param name="Reason">Label of the reason paragraph.</param>
/// <param name="TotalCredited">Label of the total, in place of the plain total.</param>
/// <param name="StatusDraft">Status text of a draft.</param>
/// <param name="StatusIssued">Status text of an issued note.</param>
/// <param name="StatusApplied">Status text of an applied note.</param>
/// <param name="StatusVoided">Status text of a voided note.</param>
/// <param name="StampDraft">The stamp across a draft.</param>
/// <param name="StampVoided">The stamp across a voided note.</param>
public sealed record CreditNotePdfLabels(
    DocumentPdfLabels Document,
    string Title,
    string IssueDate,
    string Invoice,
    string Status,
    string Reason,
    string TotalCredited,
    string StatusDraft,
    string StatusIssued,
    string StatusApplied,
    string StatusVoided,
    string StampDraft,
    string StampVoided);
