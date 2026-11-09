using Innovayse.Hostpanel.Application.Domains.Commands.AddDnsRecord;
using Innovayse.Hostpanel.Application.Domains.Commands.AddEmailForwardingRule;
using Innovayse.Hostpanel.Application.Domains.Commands.DeleteDnsRecord;
using Innovayse.Hostpanel.Application.Domains.Commands.DeleteEmailForwardingRule;
using Innovayse.Hostpanel.Application.Domains.Commands.ModifyDomainContact;
using Innovayse.Hostpanel.Application.Domains.Commands.SetDnsManagement;
using Innovayse.Hostpanel.Application.Domains.Commands.SetEmailForwarding;
using Innovayse.Hostpanel.Application.Domains.Commands.SetRegistrarLock;
using Innovayse.Hostpanel.Application.Domains.Commands.SetWhoisPrivacy;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateDnsRecord;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateEmailForwardingRule;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateNameservers;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetDomainContact;
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
/// Admin management of a domain's DNS records, nameservers, email forwarding, WHOIS
/// privacy, registrar transfer-lock and registrant contact details. Listing, registration,
/// transfer, renewal and WHOIS lookup live in <see cref="DomainsController"/>; the client-facing
/// <c>My*</c> surface lives in its own controller.
/// </summary>
[Route("api/domains")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class DomainDnsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public DomainDnsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Enables or disables WHOIS privacy for a domain.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no domain; 502 when the registrar rejects the
    /// update; 401 without a session; 403 for a caller who is not staff;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there or there is no privacy subscription; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/whois-privacy")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetWhoisPrivacyAsync(
        int id, [FromBody] SetWhoisPrivacyCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Enables or disables the registrar transfer-lock for a domain.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no domain; 502 when the registrar rejects the
    /// update; 401 without a session; 403 for a caller who is not staff;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/lock")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetRegistrarLockAsync(
        int id, [FromBody] SetRegistrarLockCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Replaces the nameserver list for a domain.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">New nameserver list (minimum 2 entries).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when fewer than 2 nameservers are given; 404 when the id names no
    /// domain; 502 when the registrar rejects the update; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/nameservers")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateNameserversAsync(
        int id, [FromBody] UpdateNameserversCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds a new DNS record to a domain's zone.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with a link to the domain; 400 when a field is refused; 404 when the id names no
    /// domain; 502 when the registrar rejects the addition; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPost("{id:int}/dns")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddDnsRecordAsync(
        int id, [FromBody] AddDnsRecordCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return outcome.IsFailure ? MapFailure(outcome) : CreatedResult($"/api/domains/{id}", (object?)null);
    }

    /// <summary>Updates an existing DNS record in a domain's zone.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="recordId">DNS record primary key; wins over any record id in the body.</param>
    /// <param name="command">Updated DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a field is refused; 404 when the domain or record id names
    /// nothing; 502 when the registrar rejects the update; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/dns/{recordId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateDnsRecordAsync(
        int id, int recordId, [FromBody] UpdateDnsRecordCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id, RecordId = recordId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a DNS record from a domain's zone.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="recordId">DNS record primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the domain or record id names nothing; 502 when the registrar
    /// rejects the deletion; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpDelete("{id:int}/dns/{recordId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteDnsRecordAsync(int id, int recordId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteDnsRecordCommand(id, recordId), ct);
        return HandleResult(outcome);
    }

    /// <summary>Reads the current WHOIS registrant contact the registrar holds for a domain.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the contact; 404 with <c>DOMAIN_NOT_FOUND</c> when the id names no domain, or
    /// <c>DOMAIN_CONTACT_NOT_FOUND</c> when the registrar holds no contact for it; 401 without a session; 403 for a caller without access;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/contact")]
    [Authorize(Policy = Policies.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(DomainContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContactAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DomainContactDto>>(new GetDomainContactQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Modifies WHOIS registrant contact details at the registrar.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Updated registrant contact details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no domain; 502 when the registrar rejects the
    /// update; 401 without a session; 403 for a caller who is not staff;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPost("{id:int}/modify-contact")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ModifyContactAsync(
        int id, [FromBody] ModifyDomainContactCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Toggles DNS management for a domain.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no domain; 502 when the registrar rejects the
    /// update; 401 without a session; 403 for a caller who is not staff;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/dns-management")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetDnsManagementAsync(
        int id, [FromBody] SetDnsManagementCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Toggles email forwarding for a domain.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the id names no domain; 502 when the registrar rejects the
    /// update; 401 without a session; 403 for a caller who is not staff;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/email-forwarding-toggle")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetEmailForwardingAsync(
        int id, [FromBody] SetEmailForwardingCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an email forwarding rule.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 on success; 400 when a field is refused; 404 when the id names no domain; 502 when the
    /// registrar rejects the addition; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPost("{id:int}/email-forwarding")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddEmailForwardingRuleAsync(
        int id, [FromBody] AddEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return outcome.IsFailure ? MapFailure(outcome) : CreatedResult($"/api/domains/{id}", (object?)null);
    }

    /// <summary>Updates an email forwarding rule.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="ruleId">Email forwarding rule primary key; wins over any rule id in the body.</param>
    /// <param name="command">Updated email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when a field is refused; 404 when the domain or rule id names nothing;
    /// 502 when the registrar rejects the update; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/email-forwarding/{ruleId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateEmailForwardingRuleAsync(
        int id, int ruleId, [FromBody] UpdateEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id, RuleId = ruleId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes an email forwarding rule.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 when the domain or rule id names nothing; 502 when the registrar
    /// rejects the deletion; 401 without a session; 403 for a caller who is not staff;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpDelete("{id:int}/email-forwarding/{ruleId:int}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteEmailForwardingRuleAsync(int id, int ruleId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteEmailForwardingRuleCommand(id, ruleId), ct);
        return HandleResult(outcome);
    }
}
