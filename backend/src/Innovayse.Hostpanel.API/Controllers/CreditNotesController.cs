using Innovayse.Hostpanel.Application.Billing.Commands.IssueCreditNote;
using Innovayse.Hostpanel.Application.Billing.Commands.VoidCreditNote;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNote;
using Innovayse.Hostpanel.Application.Billing.Queries.GetCreditNotePdf;
using Innovayse.Hostpanel.Application.Billing.Queries.ListCreditNotes;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for credit notes: issuing one against a paid invoice, listing, reading, downloading as a PDF and voiding.
/// A credit note is not an invoice credit; it reverses part of an invoice that was already paid.
/// </summary>
[Route("api/billing/credit-notes")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class CreditNotesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public CreditNotesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists credit notes, newest first, optionally of one invoice, one client or one status.</summary>
    /// <param name="query">The filters and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when a filter is refused; 401 without a session; 403 for a caller who is not staff;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(PaginatedList<CreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListCreditNotesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<CreditNoteDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns one credit note with its lines.</summary>
    /// <param name="id">Credit note primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the note; 400 for an id that is not positive; 401 without a session; 403 for a caller who is not staff;
    /// 404 with <c>CREDIT_NOTE_NOT_FOUND</c> when the id names no note; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(CreditNoteDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<CreditNoteDetailDto>>(new GetCreditNoteQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Downloads a credit note as a PDF, printed in the language of its client.</summary>
    /// <param name="id">Credit note primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>credit-note-{number}.pdf</c> as an attachment; 400 for an id that is not positive, or with
    /// <c>INVOICE_PDF_EXCEEDS_LIMIT</c> when the note has more lines than <c>InvoicePdf:MaxLines</c>; 401 without a session;
    /// 403 for a caller who is not staff; 404 with <c>CREDIT_NOTE_NOT_FOUND</c> when the id names no note; 429 when the rate
    /// limit is hit; 503 with <c>INVOICE_PDF_RENDER_BUSY</c> and <c>Retry-After</c> when no render slot frees in time.
    /// </returns>
    [HttpGet("{id:int}/pdf")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPdfAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FileDownloadDto>>(new GetCreditNotePdfQuery(id), ct);
        return FileResult(outcome);
    }

    /// <summary>
    /// Issues a credit note against a paid invoice and gives its value back to the client in the same operation, as account
    /// credit or as a gateway refund.
    /// </summary>
    /// <param name="invoiceId">The paid invoice to credit; wins over any invoice id in the body.</param>
    /// <param name="command">The lines credited, how the value goes back and why.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new note id; 400 when a value is refused, the invoice is not paid (<c>INVOICE_NOT_PAID</c>), is a credit
    /// top-up (<c>CREDIT_NOTE_TOP_UP_INVOICE</c>) or the note is more than it can still be credited
    /// (<c>CREDIT_NOTE_EXCEEDS_CREDITABLE</c>, <c>INVOICE_REFUND_EXCEEDS_PAID</c>); 401 without a session; 403 for a caller who is
    /// not an admin; 404 when the id names no invoice; 409 when another refund or credit note of the invoice was saved first
    /// (<c>INVOICE_REFUND_CONFLICT</c> or the concurrency conflict) or <c>REFUND_BLOCKED_BY_PLUGIN</c>; 502 with
    /// <c>GATEWAY_REFUND_REFUSED</c> when the gateway refuses the refund; 503 when the gateway plugin of the invoice is not
    /// configured; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("~/api/billing/{invoiceId:int}/credit-notes")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> IssueAsync(
        int invoiceId, [FromBody] IssueCreditNoteCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command with { InvoiceId = invoiceId }, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/billing/credit-notes/{id}", id));
    }

    /// <summary>Voids a credit note. It keeps its number and stays on record; a note is never deleted.</summary>
    /// <param name="id">Credit note primary key; wins over any id in the body.</param>
    /// <param name="command">Why the note is voided.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when voided; 400 when the reason is refused, or with <c>CLIENT_CREDIT_INSUFFICIENT</c> when the client has already
    /// spent the credit the note gave; 401 without a session; 403 for a caller who is not an admin; 404 with
    /// <c>CREDIT_NOTE_NOT_FOUND</c> when the id names no note; 409 with <c>CREDIT_NOTE_ALREADY_VOIDED</c>,
    /// <c>CREDIT_NOTE_REFUND_NOT_VOIDABLE</c> for a note paid out as a gateway refund, or the concurrency conflict;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/void")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VoidAsync(int id, [FromBody] VoidCreditNoteCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { CreditNoteId = id }, ct);
        return HandleResult(outcome);
    }
}
