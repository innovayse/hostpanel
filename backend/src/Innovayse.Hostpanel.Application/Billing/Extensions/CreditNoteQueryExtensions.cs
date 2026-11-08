using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Extensions;

/// <summary>
/// The one definition of which credit notes reverse tax, so every tax report that nets them agrees on which notes count and on
/// which day.
/// </summary>
/// <remarks>
/// A credit note is a correction made on the day it is issued, so the tax it reverses comes off the period of its issue date
/// (<see cref="CreditNote.IssuedAt"/>) and never the period of the invoice it corrects. A note given as account credit moves no
/// cash and leaves no refund row, so it must be read from here. A gateway refund note is left out: its refund is already one
/// ordinary refund row of its invoice, and reading the note too would count it twice. A draft or voided note reverses nothing.
/// </remarks>
public static class CreditNoteQueryExtensions
{
    /// <summary>Keeps the credit notes whose tax is reversed by the note itself: issued or applied account credit notes.</summary>
    /// <param name="notes">The credit notes.</param>
    /// <returns>The filtered query; every note kept has an issue date.</returns>
    public static IQueryable<CreditNote> ReversingTax(this IQueryable<CreditNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return notes.Where(n => n.Method == CreditNoteMethod.AccountCredit
            && (n.Status == CreditNoteStatus.Issued || n.Status == CreditNoteStatus.Applied)
            && n.IssuedAt != null);
    }

    /// <summary>Keeps the credit notes issued inside a half-open window.</summary>
    /// <param name="notes">The credit notes.</param>
    /// <param name="from">Start of the window (UTC), inclusive.</param>
    /// <param name="to">End of the window (UTC), exclusive.</param>
    /// <returns>The filtered query; the bounds compare the date column itself, with no function over it, so the filter stays a plain range comparison.</returns>
    public static IQueryable<CreditNote> IssuedWithin(this IQueryable<CreditNote> notes, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(notes);

        return notes.Where(n => n.IssuedAt >= from && n.IssuedAt < to);
    }
}
