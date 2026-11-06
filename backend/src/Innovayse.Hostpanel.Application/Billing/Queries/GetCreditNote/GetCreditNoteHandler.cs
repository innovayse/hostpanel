using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNote;

/// <summary>Returns one credit note with its lines for staff.</summary>
public sealed class GetCreditNoteHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the client name.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the client name.</param>
    public GetCreditNoteHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetCreditNoteQuery"/>.</summary>
    /// <param name="query">The note to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the note and its lines, or <c>CREDIT_NOTE_NOT_FOUND</c>.</returns>
    public async Task<Result<CreditNoteDetailDto>> HandleAsync(GetCreditNoteQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var note = await _context.CreditNotes
            .AsNoTracking()
            .Include(n => n.Items)
            .FirstOrDefaultAsync(n => n.Id == query.CreditNoteId, ct);
        if (note is null)
        {
            return Result<CreditNoteDetailDto>.FailureCode(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        }

        var client = await _bus.GetRequiredClientBriefAsync(note.ClientId, withEmail: false, ct);
        return Result.Success(new CreditNoteDetailDto(
            CreditNoteMapper.ToDto(note, client.FullName),
            note.Items.OrderBy(i => i.Id).Select(CreditNoteMapper.ToItemDto).ToList()));
    }
}
