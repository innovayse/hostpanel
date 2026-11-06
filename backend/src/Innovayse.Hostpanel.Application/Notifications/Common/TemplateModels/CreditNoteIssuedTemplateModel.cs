namespace Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;

/// <summary>Data for the <c>credit-note-issued</c> email: a credit note was issued to the client.</summary>
/// <param name="CreditNote">The note, read as <c>{{ credit_note.* }}</c>.</param>
public sealed record CreditNoteIssuedTemplateModel(CreditNoteTemplateData CreditNote) : EmailTemplateModel;
