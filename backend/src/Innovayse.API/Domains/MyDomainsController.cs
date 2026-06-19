namespace Innovayse.API.Domains;

using System.Security.Claims;
using Innovayse.API.Domains.Requests;
using Innovayse.Application.Domains.Commands.AddDnsRecord;
using Innovayse.Application.Domains.Commands.AddEmailForwardingRule;
using Innovayse.Application.Domains.Commands.DeleteDnsRecord;
using Innovayse.Application.Domains.Commands.DeleteEmailForwardingRule;
using Innovayse.Application.Domains.Commands.InitiateOutgoingTransfer;
using Innovayse.Application.Domains.Commands.ModifyDomainContact;
using Innovayse.Application.Domains.Commands.RenewDomain;
using Innovayse.Application.Domains.Commands.SetAutoRenew;
using Innovayse.Application.Domains.Commands.SetDnsManagement;
using Innovayse.Application.Domains.Commands.SetEmailForwarding;
using Innovayse.Application.Domains.Commands.SetRegistrarLock;
using Innovayse.Application.Domains.Commands.SetWhoisPrivacy;
using Innovayse.Application.Domains.Commands.UpdateDnsRecord;
using Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;
using Innovayse.Application.Domains.Commands.UpdateNameservers;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Application.Domains.Queries.GetDomain;
using Innovayse.Application.Domains.Queries.GetMyDomains;
using Innovayse.Application.Domains.Queries.GetWhois;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Domains;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using ApiRenewDomainRequest = Innovayse.API.Domains.Requests.RenewDomainRequest;

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
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

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
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new SetWhoisPrivacyCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Enables or disables the registrar transfer-lock for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRegistrarLockAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new SetRegistrarLockCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Returns the nameserver list for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ordered list of nameserver DTOs.</returns>
    [HttpGet("{id:int}/nameservers")]
    [ProducesResponseType(typeof(IReadOnlyList<NameserverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NameserverDto>>> GetNameserversAsync(int id, CancellationToken ct)
    {
        var domain = await GetOwnedDomainAsync(id, ct);
        if (domain is null)
        {
            return Forbid();
        }

        return Ok(domain.Nameservers);
    }

    /// <summary>Replaces the nameserver list for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">New nameserver list (minimum 2 entries).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/nameservers")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNameserversAsync(
        int id,
        UpdateNameserversRequest req,
        CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new UpdateNameserversCommand(id, req.Nameservers), ct);
        return NoContent();
    }

    /// <summary>Performs a WHOIS lookup for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WHOIS information for the domain.</returns>
    [HttpGet("{id:int}/whois")]
    [ProducesResponseType(typeof(WhoisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WhoisDto>> GetWhoisAsync(int id, CancellationToken ct)
    {
        var domain = await GetOwnedDomainAsync(id, ct);
        if (domain is null)
        {
            return Forbid();
        }

        var fullName = domain.Name + domain.Tld;
        var result = await bus.InvokeAsync<WhoisDto>(new GetWhoisQuery(fullName), ct);
        return Ok(result);
    }

    /// <summary>Modifies WHOIS registrant contact details for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Updated registrant contact details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/whois")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModifyWhoisContactAsync(
        int id,
        ModifyContactRequest req,
        CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new ModifyDomainContactCommand(id, new DomainContact(
            req.FirstName, req.LastName, req.Organization, req.Email, req.Phone,
            req.Address1, req.Address2, req.City, req.State, req.PostalCode, req.Country)), ct);
        return NoContent();
    }

    /// <summary>Retrieves the EPP authorization code for an outgoing transfer of a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The EPP authorization code to hand to the gaining registrar.</returns>
    [HttpPost("{id:int}/epp")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEppCodeAsync(int id, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        var eppCode = await bus.InvokeAsync<string>(new InitiateOutgoingTransferCommand(id), ct);
        return Ok(new { eppCode });
    }

    /// <summary>Renews a domain registration for additional years on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Renewal details specifying the number of years.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/renew")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenewAsync(int id, ApiRenewDomainRequest req, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new RenewDomainCommand(id, req.Years), ct);
        return NoContent();
    }

    /// <summary>Adds a new DNS record to a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with a link to the domain.</returns>
    [HttpPost("{id:int}/dns")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddDnsRecordAsync(int id, AddDnsRecordRequest req, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(
            new AddDnsRecordCommand(id, req.Type, req.Host, req.Value, req.Ttl, req.Priority),
            ct);
        return Created($"/api/me/domains/{id}", null);
    }

    /// <summary>Updates an existing DNS record in a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="recordId">DNS record primary key.</param>
    /// <param name="req">Updated DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/dns/{recordId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDnsRecordAsync(
        int id,
        int recordId,
        UpdateDnsRecordRequest req,
        CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(
            new UpdateDnsRecordCommand(id, recordId, req.Value, req.Ttl, req.Priority),
            ct);
        return NoContent();
    }

    /// <summary>Deletes a DNS record from a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="recordId">DNS record primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}/dns/{recordId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDnsRecordAsync(int id, int recordId, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new DeleteDnsRecordCommand(id, recordId), ct);
        return NoContent();
    }

    /// <summary>Toggles DNS management for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/dns-management")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDnsManagementAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new SetDnsManagementCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Toggles email forwarding for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/email-forwarding-toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEmailForwardingAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new SetEmailForwardingCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Adds an email forwarding rule to a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created on success.</returns>
    [HttpPost("{id:int}/email-forwarding")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEmailForwardingRuleAsync(
        int id,
        EmailForwardingRuleRequest req,
        CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new AddEmailForwardingRuleCommand(id, req.Source, req.Destination), ct);
        return Created($"/api/me/domains/{id}", null);
    }

    /// <summary>Updates an email forwarding rule for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="req">Updated email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/email-forwarding/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmailForwardingRuleAsync(
        int id,
        int ruleId,
        EmailForwardingRuleRequest req,
        CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(
            new UpdateEmailForwardingRuleCommand(id, ruleId, req.Source, req.Destination, req.IsActive),
            ct);
        return NoContent();
    }

    /// <summary>Deletes an email forwarding rule from a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}/email-forwarding/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmailForwardingRuleAsync(int id, int ruleId, CancellationToken ct)
    {
        var ownership = await VerifyOwnershipAsync(id, ct);
        if (ownership is not null)
        {
            return ownership;
        }

        await bus.InvokeAsync(new DeleteEmailForwardingRuleCommand(id, ruleId), ct);
        return NoContent();
    }

    /// <summary>
    /// Extracts the authenticated user's Identity ID from JWT claims.
    /// </summary>
    /// <returns>The user ID string, or <see langword="null"/> if the claim is missing.</returns>
    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    /// <summary>
    /// Verifies that the specified domain belongs to the authenticated client.
    /// Returns an <see cref="IActionResult"/> (Unauthorized or Forbid) on failure, or <see langword="null"/> on success.
    /// </summary>
    /// <param name="domainId">Domain primary key to verify ownership for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An error action result if ownership cannot be verified; <see langword="null"/> if the client owns the domain.</returns>
    private async Task<IActionResult?> VerifyOwnershipAsync(int domainId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var myDomains = await bus.InvokeAsync<IReadOnlyList<DomainDto>>(new GetMyDomainsQuery(userId), ct);
        if (myDomains.All(d => d.Id != domainId))
        {
            return Forbid();
        }

        return null;
    }

    /// <summary>
    /// Retrieves a domain by its primary key after verifying ownership by the authenticated client.
    /// Returns <see langword="null"/> if the user is unauthenticated or the domain does not belong to them.
    /// </summary>
    /// <param name="domainId">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain DTO if owned by the authenticated client; otherwise <see langword="null"/>.</returns>
    private async Task<DomainDto?> GetOwnedDomainAsync(int domainId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return null;
        }

        var myDomains = await bus.InvokeAsync<IReadOnlyList<DomainDto>>(new GetMyDomainsQuery(userId), ct);
        var domain = myDomains.FirstOrDefault(d => d.Id == domainId);
        if (domain is null)
        {
            return null;
        }

        // GetMyDomains may return a summary — fetch the full domain with nested data.
        return await bus.InvokeAsync<DomainDto>(new GetDomainQuery(domainId), ct);
    }
}
