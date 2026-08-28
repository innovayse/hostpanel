namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>
/// A section of the export this platform holds no data for, carrying the sentence that says so.
/// Used by <c>consentHistory</c> and <c>payMethods</c>: both are offered as checkboxes in the
/// admin export dialog, so omitting them from the file would read as a bug rather than as
/// "nothing of the sort is stored here".
/// </summary>
/// <param name="Note">The explanation shown in place of the missing data.</param>
public sealed record ClientExportNoteDto(string Note);
