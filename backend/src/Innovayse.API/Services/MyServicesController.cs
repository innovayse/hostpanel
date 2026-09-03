namespace Innovayse.API.Services;

using Innovayse.API.Provisioning.Requests;
using Innovayse.API.Services.Requests;
using Innovayse.Application.Billing.Queries.GetMyServiceInvoices;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Application.Provisioning.Commands.ChangeMyServicePassword;
using Innovayse.Application.Provisioning.Queries.GetMyServiceCPanelSsoUrl;
using Innovayse.Application.Services.Commands.CancelMyService;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Application.Services.Commands.SetupMyService;
using Innovayse.Application.Services.Queries.GetCancellationStatus;
using Innovayse.Application.Services.Queries.GetMyServiceCancellationStatus;
using Innovayse.Application.Services.Queries.GetMyServices;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for viewing and ordering services.
/// Requires Client role.
/// </summary>
/// <remarks>
/// <para>
/// No action here checks ownership, and none may: the rule lives in the client-facing
/// <c>My*</c> handlers, which resolve the caller from the credential themselves. That is not
/// only the thin-controller rule — two of the use cases behind these routes
/// (<c>GetCPanelSsoUrlQuery</c>, <c>ChangePasswordCommand</c>) are also dispatched by the admin
/// <c>ProvisioningController</c>, where acting on any client's service is legitimate, so a check
/// written here would have guaranteed nothing about the message itself and a check written on
/// the shared use case would have broken the admin path.
/// </para>
/// <para>
/// Every action below that takes a service id off the route dispatches a message implementing
/// <see cref="Innovayse.Application.Services.Common.ICallerScopedServiceMessage"/>, and a
/// reflection test enforces that of any action added later. A service belonging to somebody
/// else, a service that does not exist, and a caller with no client record all answer 404 with
/// <c>MY_SERVICE_NOT_FOUND</c> and the same sentence; service ids are sequential, and telling
/// those three apart is itself a way of enumerating them.
/// </para>
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/me/services")]
[Authorize(Roles = Roles.Client)]
public sealed class MyServicesController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all services belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of client service DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientServiceDto>>> GetMineAsync(CancellationToken ct)
    {
        var services = await bus.InvokeAsync<IReadOnlyList<ClientServiceDto>>(
            new GetMyServicesQuery(), ct);
        return Ok(services);
    }

    /// <summary>Orders a new service for the authenticated client.</summary>
    /// <param name="request">Order request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new service ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> OrderAsync(
        [FromBody] OrderRequest request, CancellationToken ct)
    {
        // OrderServiceCommand names a client because the order-fulfilment handlers dispatch it
        // for whichever account an accepted order belongs to. On this route the account is the
        // caller's own, and it is read back from the credential-scoped profile query rather
        // than from anything the request could carry.
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        var cmd = new OrderServiceCommand(profile.Id, request.ProductId, request.BillingCycle, 0, 0);
        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>
    /// Generates a time-limited cPanel SSO URL for a service owned by the authenticated client.
    /// </summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK with <c>{ url }</c> carrying the control-panel SSO address; 404 with
    /// <c>MY_SERVICE_NOT_FOUND</c> when the service is not the caller's.
    /// </returns>
    [HttpGet("{id:int}/cpanel-sso")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCPanelSsoUrlAsync(int id, CancellationToken ct)
    {
        var url = await bus.InvokeAsync<string>(new GetMyServiceCPanelSsoUrlQuery(id), ct);

        // `{ url }`, matching the staff route in ProvisioningController. This answered with a
        // bare JSON string while its sibling answered with an object, and the portal — written
        // against the sibling — destructured `url` off a string, got undefined, and opened a
        // blank tab. Two shapes for one answer is what made that possible.
        return Ok(new { url });
    }

    /// <summary>Submits a cancellation request for a client service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Cancellation request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK on success; 404 with <c>MY_SERVICE_NOT_FOUND</c> when the service is not the
    /// caller's.
    /// </returns>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(
        int id, [FromBody] CancelServiceRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new CancelMyServiceCommand(id, request.Type, request.Reason), ct);
        return Ok();
    }

    /// <summary>
    /// Sets up a pending service with hosting details (domain, username, password)
    /// and triggers provisioning.
    /// </summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Setup request body with domain, username, and password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK on successful setup and provisioning; 404 with <c>MY_SERVICE_NOT_FOUND</c> when the
    /// service is not the caller's.
    /// </returns>
    [HttpPost("{id:int}/setup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetupAsync(
        int id, [FromBody] SetupServiceRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new SetupMyServiceCommand(id, request.Domain, request.Username, request.Password), ct);
        return Ok();
    }

    /// <summary>
    /// Returns the invoices charged to a service owned by the authenticated client.
    /// </summary>
    /// <remarks>
    /// The only action on this controller whose use case scopes itself to the caller. The reply
    /// carries the count of the caller's unattributable invoices alongside the list, because an
    /// empty list here means "nothing is recorded against this service", which is a weaker claim
    /// than "nothing was charged" — see <see cref="ServiceInvoicesDto"/>.
    /// </remarks>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK with the service's invoices; 404 with <c>MY_SERVICE_NOT_FOUND</c> when the service
    /// is not the caller's.
    /// </returns>
    [HttpGet("{id:int}/invoices")]
    [ProducesResponseType(typeof(ServiceInvoicesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceInvoicesDto>> GetInvoicesAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ServiceInvoicesDto>(new GetMyServiceInvoicesQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Returns the cancellation status for a client service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK with cancellation status DTO; 404 with <c>MY_SERVICE_NOT_FOUND</c> when the service
    /// is not the caller's.
    /// </returns>
    [HttpGet("{id:int}/cancellation-status")]
    [ProducesResponseType(typeof(CancellationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancellationStatusDto>> GetCancellationStatusAsync(
        int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CancellationStatusDto>(
            new GetMyServiceCancellationStatusQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Changes the hosting account password for a client service.</summary>
    /// <param name="id">Client service primary key.</param>
    /// <param name="request">Request body containing the new password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK on success; 404 with <c>MY_SERVICE_NOT_FOUND</c> when the service is not the
    /// caller's.
    /// </returns>
    [HttpPost("{id:int}/change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePasswordAsync(
        int id, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(new ChangeMyServicePasswordCommand(id, request.NewPassword), ct);
        return Ok();
    }
}
