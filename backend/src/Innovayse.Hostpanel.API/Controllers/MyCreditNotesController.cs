using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyCreditNotePdf;
using Innovayse.Hostpanel.Application.Billing.Queries.ListMyCreditNotes;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client portal endpoints for the credit notes issued to the authenticated client, and their PDF. Every action behind the client
/// portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>) when the enabled identity
/// plugin cannot say which account the caller acts for; the convention <c>IdentityPluginUnavailableConvention</c> declares it
/// once in the generated document.
/// </summary>
/// <remarks>
/// No route here names a client and no action reads a claim. A note of somebody else, a draft and a note that does not exist
/// answer alike, because ids here are sequential and telling those apart is itself a way of enumerating them.
/// </remarks>
[Route("api/me/credit-notes")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyCreditNotesController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyCreditNotesController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the credit notes issued to the authenticated client, newest first.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the caller's notes; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks
    /// <c>ViewPayInvoices</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record; 429 when the rate
    /// limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(IReadOnlyList<MyCreditNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<MyCreditNoteDto>>>(new ListMyCreditNotesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Downloads one of the authenticated client's credit notes as a PDF, printed in their language.</summary>
    /// <param name="id">Credit note primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with <c>credit-note-{number}.pdf</c> as an attachment; 400 for an id that is not positive, or with
    /// <c>INVOICE_PDF_EXCEEDS_LIMIT</c> when the note has more lines than <c>InvoicePdf:MaxLines</c>; 401 without a session;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>; 404 with <c>CREDIT_NOTE_NOT_FOUND</c>
    /// when it is not the caller's or is still a draft; 429 when the caller already has as many renders in flight as the
    /// concurrency policy allows; 503 with <c>INVOICE_PDF_RENDER_BUSY</c> and <c>Retry-After</c> when no render slot frees in time.
    /// </returns>
    [HttpGet("{id:int}/pdf")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [EnableRateLimiting(RateLimitPolicies.Concurrent)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPdfAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FileDownloadDto>>(new GetMyCreditNotePdfQuery(id), ct);
        return FileResult(outcome);
    }
}
