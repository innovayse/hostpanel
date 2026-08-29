namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.API.RateLimiting;
using Innovayse.Application.Support.Commands.SendContactMessage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

/// <summary>
/// The public website's contact form.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous by design -- this is the form a visitor with no account fills in -- and a controller
/// of its own rather than another action on <c>DepartmentsController</c> or
/// <c>MyTicketsController</c>: neither of those is anonymous surface, and hanging a world-reachable
/// action off a class whose <c>[Authorize]</c> says otherwise is how the next action added there
/// ends up public by accident. <c>PublishedAnnouncementsController</c> splits for the same reason.
/// </para>
/// <para>
/// <b>Rate limited, and the tightest tier there is.</b> This paragraph used to record that the
/// solution registered no rate limiting at all and that doing it properly was its own piece of
/// work; that work is done. Every request now passes the global budget, and this class is one of
/// the few that names a tighter one: <see cref="RateLimitPolicies.Strict"/>, five a minute.
/// The reason it is that tight and the domain lookups are not is that the damage here is not
/// load -- a flood fills an operator's inbox and chat and can burn the relay's sending
/// reputation, all long before the server notices -- and nobody legitimately submits an enquiry
/// form five times in a minute.
/// <para>
/// One caveat worth knowing before tuning it down further: the public site reaches this through
/// the portal's Nuxt server, which calls the API itself, so every anonymous visitor shares one
/// partition on that path. The tier binds on a caller hitting <c>/api/contact</c> directly --
/// which is what an abuser does -- and the shared bucket is why it is not lower still.
/// </para>
/// </para>
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/contact")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Strict)]
public sealed class ContactController(IMessageBus bus) : ControllerBase
{
    /// <summary>Relays a contact-form submission to the operator's enquiry inbox.</summary>
    /// <param name="request">The submitted form.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 No Content once the SMTP relay has accepted the message -- and only then. A refusal or
    /// a delivery failure travels as an exception, which <c>ExceptionMiddleware</c> turns into a
    /// status carrying a code the page branches on; there is no shape of this response that means
    /// "accepted but not sent".
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> SendAsync([FromBody] SendContactMessageRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new SendContactMessageCommand(
                request.Name, request.Email, request.Phone, request.Service, request.Message, request.SubmittedAt),
            ct);

        return NoContent();
    }
}
