namespace Innovayse.API.Domains;

using Innovayse.Application.Domains.DTOs;
using Innovayse.Application.Domains.Queries.CheckDomainAvailability;
using Innovayse.Application.Domains.Queries.GetWhois;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Public domain lookup endpoints accessible to any authenticated user (Admin, Reseller, or Client).
/// These are read-only queries that do not require elevated privileges.
/// </summary>
/// <param name="bus">Wolverine message bus for dispatching queries.</param>
[ApiController]
[Route("api/domains")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller},{Roles.Client}")]
public sealed class DomainLookupController(IMessageBus bus) : ControllerBase
{
    /// <summary>Checks whether a domain name is available for registration.</summary>
    /// <param name="name">The fully-qualified domain name to check (e.g. "example.com").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the domain is available; otherwise <see langword="false"/>.</returns>
    [HttpGet("check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<bool>> CheckAvailabilityAsync(
        [FromQuery] string name,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<bool>(new CheckDomainAvailabilityQuery(name), ct);
        return Ok(result);
    }
}
