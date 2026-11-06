using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListMyCreditNotes;

/// <summary>Lists the credit notes issued to the account the caller acts for, newest first.</summary>
/// <remarks>
/// A client sees a note once it has its number (<see cref="CreditNote.Number"/>); a draft never reaches a client. The account
/// is resolved from the credential, so no id travels in the request and a note of another client cannot be named.
/// </remarks>
public sealed class ListMyCreditNotesHandler
{
    /// <summary>The module data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the caller acts for, owner or linked user; the query does not say, and must not.</summary>
    private readonly IClientAccess _access;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module data context.</param>
    /// <param name="access">Resolves the account the caller acts for.</param>
    public ListMyCreditNotesHandler(IHostpanelDbContext context, IClientAccess access)
    {
        _context = context;
        _access = access;
    }

    /// <summary>Handles <see cref="ListMyCreditNotesQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the notes, or <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account.</returns>
    public async Task<Result<IReadOnlyList<MyCreditNoteDto>>> HandleAsync(ListMyCreditNotesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<IReadOnlyList<MyCreditNoteDto>>.FailureCode(clientId.ErrorCode!);
        }

        var notes = await _context.CreditNotes
            .AsNoTracking()
            .Where(n => n.ClientId == clientId.Value && n.Number != null)
            .OrderByDescending(n => n.IssuedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync(ct);

        IReadOnlyList<MyCreditNoteDto> dtos = notes.Select(CreditNoteMapper.ToMyDto).ToList();
        return Result.Success(dtos);
    }
}
