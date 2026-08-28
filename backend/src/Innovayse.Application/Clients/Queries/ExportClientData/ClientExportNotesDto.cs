namespace Innovayse.Application.Clients.Queries.ExportClientData;

/// <summary>The <c>notes</c> section of a client data export.</summary>
/// <param name="AdminNotes">Free-text notes staff kept on the account; null when none were written.</param>
public sealed record ClientExportNotesDto(string? AdminNotes);
