using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>Maps a <see cref="CreditNote"/> to the shapes staff and clients read.</summary>
public static class CreditNoteMapper
{
    /// <summary>Maps a note to the row staff see.</summary>
    /// <param name="note">The credit note.</param>
    /// <param name="clientName">Display name of the client it is issued to.</param>
    /// <returns>The mapped DTO.</returns>
    public static CreditNoteDto ToDto(CreditNote note, string clientName)
    {
        ArgumentNullException.ThrowIfNull(note);

        return new CreditNoteDto(
            note.Id,
            note.Number,
            note.Status,
            note.Method,
            note.ClientId,
            clientName,
            note.InvoiceId,
            note.Currency,
            note.SubTotal,
            note.TaxRate,
            note.Tax,
            note.Total,
            note.Reason,
            note.RefundReference,
            note.CreatedAt,
            note.IssuedAt,
            note.AppliedAt,
            note.VoidedAt,
            note.VoidReason);
    }

    /// <summary>Maps a note line.</summary>
    /// <param name="item">The credit note line.</param>
    /// <returns>The mapped DTO.</returns>
    public static CreditNoteItemDto ToItemDto(CreditNoteItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new CreditNoteItemDto(item.Id, item.Description, item.UnitPrice, item.Quantity, item.Amount);
    }

    /// <summary>Maps a note to what its client sees. Only a note that was issued has a number and an issue date.</summary>
    /// <param name="note">An issued, applied or voided credit note.</param>
    /// <returns>The mapped DTO.</returns>
    public static MyCreditNoteDto ToMyDto(CreditNote note)
    {
        ArgumentNullException.ThrowIfNull(note);

        return new MyCreditNoteDto(
            note.Id,
            note.Number!,
            note.Status,
            note.Method,
            note.InvoiceId,
            note.Currency,
            note.SubTotal,
            note.TaxRate,
            note.Tax,
            note.Total,
            note.Reason,
            note.IssuedAt!.Value,
            note.VoidedAt);
    }
}
