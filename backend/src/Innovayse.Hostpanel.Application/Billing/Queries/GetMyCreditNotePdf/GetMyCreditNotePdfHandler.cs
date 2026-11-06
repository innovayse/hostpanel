using Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNotePdf;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyCreditNotePdf;

/// <summary>
/// Renders one of the calling client's own credit notes as a PDF, refusing every id that is not theirs or has no number yet.
/// </summary>
/// <remarks>
/// Ownership is checked here, so it travels with the message. A note of another client, a draft and an id that names nothing
/// all answer <c>CREDIT_NOTE_NOT_FOUND</c> alike: ids are sequential, and telling those apart is itself a way of enumerating
/// them. Once settled, the rendering is the staff use case, so this dispatches <see cref="GetCreditNotePdfQuery"/>.
/// </remarks>
public sealed class GetMyCreditNotePdfHandler
{
    /// <summary>The module data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the caller acts for.</summary>
    private readonly IClientAccess _access;

    /// <summary>Wolverine bus, used to reach the shared rendering once access is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module data context.</param>
    /// <param name="access">Resolves the account the caller acts for.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared rendering once access is settled.</param>
    public GetMyCreditNotePdfHandler(IHostpanelDbContext context, IClientAccess access, IMessageBus bus)
    {
        _context = context;
        _access = access;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetMyCreditNotePdfQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The PDF, or <c>CREDIT_NOTE_NOT_FOUND</c> (404) when the note is not the caller's own, is a draft, does not exist, or the
    /// caller has no client record; otherwise whatever <c>GetCreditNotePdfHandler</c> answers.
    /// </returns>
    public async Task<Result<FileDownloadDto>> HandleAsync(GetMyCreditNotePdfQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            // No account and a foreign note answer alike, so the route cannot be used to probe ids.
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        }

        var owned = await _context.CreditNotes
            .AsNoTracking()
            .AnyAsync(n => n.Id == query.CreditNoteId && n.ClientId == clientId.Value && n.Number != null, ct);
        if (!owned)
        {
            return Result<FileDownloadDto>.FailureCode(HostpanelErrorCodes.CREDIT_NOTE_NOT_FOUND);
        }

        return await _bus.InvokeAsync<Result<FileDownloadDto>>(new GetCreditNotePdfQuery(query.CreditNoteId), ct);
    }
}
