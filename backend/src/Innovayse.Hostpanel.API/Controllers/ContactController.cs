using Innovayse.Hostpanel.Application.Support.Commands.SendContactMessage;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The public website's contact form.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous by design, this is the form a visitor with no account fills in, and a controller
/// of its own rather than another action on <see cref="DepartmentsController"/> or
/// <c>MyTicketsController</c>: neither of those is anonymous surface, and hanging a world-reachable
/// action off a class whose <c>[Authorize]</c> says otherwise is how the next action added there
/// ends up public by accident. <see cref="KnowledgeBaseController"/> splits from its admin sibling
/// for the same reason.
/// </para>
/// <para>
/// <b>Rate limited, and the tightest tier there is.</b> Every request passes the global budget,
/// and this class names a tighter one: <see cref="RateLimitPolicies.Strict"/>, five a minute. The
/// reason it is that tight and the domain lookups are not is that the damage here is not load, a
/// flood fills an operator's inbox and chat and can burn the relay's sending reputation, all long
/// before the server notices, and nobody legitimately submits an enquiry form five times in a
/// minute.
/// </para>
/// <para>
/// One caveat worth knowing before tuning it down further: the public site reaches this through
/// the portal's Nuxt server, which calls the API itself, so every anonymous visitor shares one
/// partition on that path. The tier binds on a caller hitting <c>/api/contact</c> directly, which
/// is what an abuser does, and the shared bucket is why it is not lower still.
/// </para>
/// </remarks>
[Route("api/contact")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Strict)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ContactController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ContactController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Relays a contact-form submission to the operator's enquiry inbox.</summary>
    /// <param name="command">The submitted form.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 No Content once the SMTP relay has accepted the message, and only then;
    /// <c>CONTACT_NOT_CONFIGURED</c> (503) when this deployment has no enquiry inbox configured;
    /// <c>CONTACT_SEND_FAILED</c> (502) when the relay refused the message; 400 when a field is
    /// missing or too long;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendAsync([FromBody] SendContactMessageCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }
}
