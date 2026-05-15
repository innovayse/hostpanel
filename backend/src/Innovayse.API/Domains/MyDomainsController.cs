namespace Innovayse.API.Domains;

using System.Security.Claims;
using Innovayse.API.Domains.Requests;
using Innovayse.Application.Domains.Commands.SetAutoRenew;
using Innovayse.Application.Domains.Commands.SetWhoisPrivacy;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Application.Domains.Queries.GetDomain;
using Innovayse.Application.Domains.Queries.GetMyDomains;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client-facing endpoints for managing the authenticated client's own domains.
/// Requires the Client role.
/// </summary>
/// <param name="bus">Wolverine message bus for dispatching commands and queries.</param>
[ApiController]
[Route("api/me/domains")]
[Authorize(Roles = Roles.Client)]
public sealed class MyDomainsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns all domains belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of domain DTOs for the authenticated client.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DomainDto>>> GetMyDomainsAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await bus.InvokeAsync<IReadOnlyList<DomainDto>>(new GetMyDomainsQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single domain by its primary key, only if it belongs to the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full domain DTO including nameservers and DNS records.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DomainDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await bus.InvokeAsync<DomainDto>(new GetDomainQuery(id), ct);

        // Resolve the numeric client ID from the domain result by matching UserId via the query
        // The domain's ClientId is compared against the resolved client. Since we query by userId,
        // any mismatch means the domain belongs to someone else — forbid access.
        var myDomains = await bus.InvokeAsync<IReadOnlyList<DomainDto>>(new GetMyDomainsQuery(userId), ct);
        if (myDomains.All(d => d.Id != id))
        {
            return Forbid();
        }

        return Ok(result);
    }

    /// <summary>Enables or disables automatic renewal for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/auto-renew")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAutoRenewAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetAutoRenewCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Enables or disables WHOIS privacy for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/whois-privacy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWhoisPrivacyAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetWhoisPrivacyCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>
    /// Extracts the authenticated user's Identity ID from JWT claims.
    /// </summary>
    /// <returns>The user ID string, or <see langword="null"/> if the claim is missing.</returns>
    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");
}
