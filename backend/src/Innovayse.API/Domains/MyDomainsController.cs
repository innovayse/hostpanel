namespace Innovayse.API.Domains;

using Innovayse.API.Domains.Requests;
using Innovayse.API.RateLimiting;
using Innovayse.Application.Domains.Commands.AddMyDomainDnsRecord;
using Innovayse.Application.Domains.Commands.AddMyDomainEmailForwardingRule;
using Innovayse.Application.Domains.Commands.DeleteMyDomainDnsRecord;
using Innovayse.Application.Domains.Commands.DeleteMyDomainEmailForwardingRule;
using Innovayse.Application.Domains.Commands.InitiateMyOutgoingTransfer;
using Innovayse.Application.Domains.Commands.ModifyMyDomainContact;
using Innovayse.Application.Domains.Commands.RenewMyDomain;
using Innovayse.Application.Domains.Commands.SetMyDomainAutoRenew;
using Innovayse.Application.Domains.Commands.SetMyDomainDnsManagement;
using Innovayse.Application.Domains.Commands.SetMyDomainEmailForwarding;
using Innovayse.Application.Domains.Commands.SetMyDomainRegistrarLock;
using Innovayse.Application.Domains.Commands.SetMyDomainWhoisPrivacy;
using Innovayse.Application.Domains.Commands.UpdateMyDomainDnsRecord;
using Innovayse.Application.Domains.Commands.UpdateMyDomainEmailForwardingRule;
using Innovayse.Application.Domains.Commands.UpdateMyDomainNameservers;
using Innovayse.Application.Domains.Common;
using Innovayse.Application.Domains.Queries.GetMyDomain;
using Innovayse.Application.Domains.Queries.GetMyDomainNameservers;
using Innovayse.Application.Domains.Queries.GetMyDomains;
using Innovayse.Application.Domains.Queries.GetMyDomainWhois;
using Innovayse.Application.Domains.Queries.GetWhois;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Domains;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;
using ApiRenewDomainRequest = Innovayse.API.Domains.Requests.RenewDomainRequest;

/// <summary>
/// Client-facing endpoints for managing the authenticated client's own domains.
/// Requires the Client role.
/// </summary>
/// <remarks>
/// <para>
/// No action here reads a claim and none checks ownership. The rule that a domain must belong to
/// the caller used to run in this file, eighteen times, ahead of the dispatch; it now lives in
/// the client-facing <c>My*</c> handlers, which resolve the caller themselves. That move is not
/// cosmetic: every command these actions used to dispatch -- <c>SetAutoRenewCommand</c>,
/// <c>RenewDomainCommand</c>, <c>AddDnsRecordCommand</c> and the rest -- is also dispatched by
/// the admin <c>DomainsController</c>, so a check that lived here guaranteed nothing about the
/// message itself.
/// </para>
/// <para>
/// A domain belonging to somebody else, a domain that does not exist, and a caller with no
/// client record all answer 404 with <c>DOMAIN_NOT_FOUND</c>. These ids are sequential, and
/// telling those three apart is itself a way of enumerating them. This replaces the 403 the
/// routes used to answer, which withheld as much but disagreed with the ticket and invoice
/// routes for no reason a reader could recover.
/// </para>
/// </remarks>
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
        var result = await bus.InvokeAsync<IReadOnlyList<DomainDto>>(new GetMyDomainsQuery(), ct);
        return Ok(result);
    }

    /// <summary>Returns a single domain belonging to the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The domain DTO; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DomainDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<DomainDto>(new GetMyDomainQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Enables or disables automatic renewal for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/auto-renew")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAutoRenewAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetMyDomainAutoRenewCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Enables or disables WHOIS privacy for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/whois-privacy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWhoisPrivacyAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetMyDomainWhoisPrivacyCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>
    /// Enables or disables the registrar transfer-lock for a domain owned by the authenticated
    /// client.
    /// </summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRegistrarLockAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetMyDomainRegistrarLockCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Returns the nameserver list for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ordered nameserver list; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpGet("{id:int}/nameservers")]
    [ProducesResponseType(typeof(IReadOnlyList<NameserverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NameserverDto>>> GetNameserversAsync(int id, CancellationToken ct)
    {
        var nameservers = await bus.InvokeAsync<IReadOnlyList<NameserverDto>>(
            new GetMyDomainNameserversQuery(id), ct);
        return Ok(nameservers);
    }

    /// <summary>Replaces the nameserver list for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">New nameserver list (minimum 2 entries).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/nameservers")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNameserversAsync(int id,
        UpdateNameserversRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateMyDomainNameserversCommand(id, req.Nameservers), ct);
        return NoContent();
    }

    /// <summary>Performs a WHOIS lookup for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WHOIS information; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpGet("{id:int}/whois")]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(WhoisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WhoisDto>> GetWhoisAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<WhoisDto>(new GetMyDomainWhoisQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Modifies WHOIS registrant contact details for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Updated registrant contact details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/whois")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModifyWhoisContactAsync(int id,
        ModifyContactRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new ModifyMyDomainContactCommand(id, new DomainContact(
            req.FirstName, req.LastName, req.Organization, req.Email, req.Phone,
            req.Address1, req.Address2, req.City, req.State, req.PostalCode, req.Country)), ct);
        return NoContent();
    }

    /// <summary>
    /// Retrieves the EPP authorization code for an outgoing transfer of a domain owned by the
    /// authenticated client.
    /// </summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The EPP authorization code; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's.</returns>
    [HttpPost("{id:int}/epp")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEppCodeAsync(int id, CancellationToken ct)
    {
        var eppCode = await bus.InvokeAsync<string>(new InitiateMyOutgoingTransferCommand(id), ct);
        return Ok(new { eppCode });
    }

    /// <summary>Renews a domain registration for additional years on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Renewal details specifying the number of years.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPost("{id:int}/renew")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenewAsync(int id, ApiRenewDomainRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new RenewMyDomainCommand(id, req.Years), ct);
        return NoContent();
    }

    /// <summary>Adds a new DNS record to a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created with a link to the domain; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the
    /// caller's.
    /// </returns>
    [HttpPost("{id:int}/dns")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddDnsRecordAsync(int id, AddDnsRecordRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new AddMyDomainDnsRecordCommand(id, req.Type, req.Host, req.Value, req.Ttl, req.Priority),
            ct);
        return Created($"/api/me/domains/{id}", null);
    }

    /// <summary>Updates an existing DNS record in a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="recordId">DNS record primary key.</param>
    /// <param name="req">Updated DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/dns/{recordId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDnsRecordAsync(int id,
        int recordId,
        UpdateDnsRecordRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateMyDomainDnsRecordCommand(id, recordId, req.Value, req.Ttl, req.Priority),
            ct);
        return NoContent();
    }

    /// <summary>Deletes a DNS record from a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="recordId">DNS record primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpDelete("{id:int}/dns/{recordId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDnsRecordAsync(int id, int recordId, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteMyDomainDnsRecordCommand(id, recordId), ct);
        return NoContent();
    }

    /// <summary>Toggles DNS management for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/dns-management")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDnsManagementAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetMyDomainDnsManagementCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Toggles email forwarding for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/email-forwarding-toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEmailForwardingAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetMyDomainEmailForwardingCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Adds an email forwarding rule to a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPost("{id:int}/email-forwarding")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEmailForwardingRuleAsync(int id,
        EmailForwardingRuleRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(
            new AddMyDomainEmailForwardingRuleCommand(id, req.Source, req.Destination), ct);
        return Created($"/api/me/domains/{id}", null);
    }

    /// <summary>Updates an email forwarding rule for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="req">Updated email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpPut("{id:int}/email-forwarding/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmailForwardingRuleAsync(int id,
        int ruleId,
        EmailForwardingRuleRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateMyDomainEmailForwardingRuleCommand(
                id, ruleId, req.Source, req.Destination, req.IsActive),
            ct);
        return NoContent();
    }

    /// <summary>Deletes an email forwarding rule from a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's.</returns>
    [HttpDelete("{id:int}/email-forwarding/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmailForwardingRuleAsync(int id, int ruleId, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteMyDomainEmailForwardingRuleCommand(id, ruleId), ct);
        return NoContent();
    }
}
