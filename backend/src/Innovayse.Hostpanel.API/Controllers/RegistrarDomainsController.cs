using Innovayse.Hostpanel.Application.Domains.Commands.ImportRegistrarDomain;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.ListRegistrarDomains;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin import of the domains a registrar account already holds: the list a registrar plugin reports, marked with what the
/// panel has, and the import of one of them into a client without an order, an invoice or a charge.
/// </summary>
[Route("api/domains/registrar")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class RegistrarDomainsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public RegistrarDomainsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Reads one page of the domains a registrar plugin reports for its account, each marked with whether the panel has it.</summary>
    /// <param name="module">The id of the installed registrar plugin to ask, in any letter case; wins over any module in the query.</param>
    /// <param name="query">The cursor of the page wanted; none reads the first page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the module is not an installed registrar plugin or the cursor is too long; 401 without a
    /// session; 403 for a caller who is not staff; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> when the registrar offers no
    /// domain list; 502 when the registrar rejects the request; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set
    /// up, or <c>REGISTRAR_ACCESS_DENIED</c> when the registrar refuses the saved credentials; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{module}")]
    [Authorize(Policy = Policies.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(RegistrarDomainListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListAsync(string module, [FromQuery] ListRegistrarDomainsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<RegistrarDomainListDto>>(query with { RegistrarModule = module }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Imports a domain a registrar account holds into a client, with its real status and expiry, without an order, invoice or charge.</summary>
    /// <param name="module">The id of the installed registrar plugin that holds the domain, in any letter case; wins over any module in the body.</param>
    /// <param name="command">The client and the domain name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with a link to the new domain; 400 when the body or route fails validation; 401 without a session; 403 for a caller who
    /// is not staff; 404 with <c>CLIENT_NOT_FOUND</c> when the id names no client, or <c>REGISTRAR_DOMAIN_NOT_FOUND</c> when the
    /// registrar's list does not hold the name; 409 with <c>DOMAIN_ALREADY_REGISTERED</c> when the panel already has the name,
    /// <c>DOMAIN_IMPORT_EXPIRY_UNKNOWN</c> when the registrar reports no expiry, or <c>REGISTRAR_OPERATION_UNSUPPORTED</c> when the
    /// registrar offers no domain list; 502 when the registrar rejects the request; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no
    /// registrar is set up, or <c>REGISTRAR_ACCESS_DENIED</c> when the registrar refuses the saved credentials; 429 when the rate
    /// limit is hit.
    /// </returns>
    [HttpPost("{module}/import")]
    [Authorize(Policy = Policies.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ImportAsync(string module, [FromBody] ImportRegistrarDomainCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command with { RegistrarModule = module }, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/domains/{id}", (object?)null));
    }
}
