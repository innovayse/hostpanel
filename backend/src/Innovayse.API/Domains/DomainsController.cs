namespace Innovayse.API.Domains;

using Innovayse.API.Domains.Requests;
using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.AddDnsRecord;
using Innovayse.Application.Domains.Commands.AddEmailForwardingRule;
using Innovayse.Application.Domains.Commands.CancelTransfer;
using Innovayse.Application.Domains.Commands.DeleteDnsRecord;
using Innovayse.Application.Domains.Commands.DeleteEmailForwardingRule;
using Innovayse.Application.Domains.Commands.InitiateOutgoingTransfer;
using Innovayse.Application.Domains.Commands.ModifyDomainContact;
using Innovayse.Application.Domains.Commands.RegisterDomain;
using Innovayse.Application.Domains.Commands.RenewDomain;
using Innovayse.Application.Domains.Commands.SetAutoRenew;
using Innovayse.Application.Domains.Commands.SetDnsManagement;
using Innovayse.Application.Domains.Commands.SetEmailForwarding;
using Innovayse.Application.Domains.Commands.SetRegistrarLock;
using Innovayse.Application.Domains.Commands.SetWhoisPrivacy;
using Innovayse.Application.Domains.Commands.TransferDomain;
using Innovayse.Application.Domains.Commands.UpdateDnsRecord;
using Innovayse.Application.Domains.Commands.UpdateDomain;
using Innovayse.Application.Domains.Commands.UpdateEmailForwardingRule;
using Innovayse.Application.Domains.Commands.UpdateNameservers;
using Innovayse.Application.Domains.DTOs;
using Innovayse.Application.Domains.Queries.CheckDomainAvailability;
using Innovayse.Application.Domains.Queries.GetDomain;
using Innovayse.Application.Domains.Queries.GetWhois;
using Innovayse.Application.Domains.Queries.ListDomains;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Domains;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using ApiRegisterDomainRequest = Innovayse.API.Domains.Requests.RegisterDomainRequest;
using ApiRenewDomainRequest = Innovayse.API.Domains.Requests.RenewDomainRequest;
using ApiTransferDomainRequest = Innovayse.API.Domains.Requests.TransferDomainRequest;

/// <summary>
/// Admin/Reseller endpoints for managing all domains in the system.
/// Requires the Admin or Reseller role.
/// </summary>
/// <param name="bus">Wolverine message bus for dispatching commands and queries.</param>
[ApiController]
[Route("api/domains")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Reseller}")]
public sealed class DomainsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all domains in the system.</summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Number of items per page (max 100).</param>
    /// <param name="clientId">Optional client ID to filter domains by owner; null returns all.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of domain list item DTOs.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DomainRegistrationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<DomainRegistrationDto>>> ListDomainsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? clientId = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<DomainRegistrationDto>>(
            new ListDomainsQuery(page, pageSize, clientId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single domain by its primary key.</summary>
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
        var result = await bus.InvokeAsync<DomainDto>(new GetDomainQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Performs a WHOIS lookup for a domain name.</summary>
    /// <param name="name">The fully-qualified domain name to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WHOIS information for the specified domain.</returns>
    [HttpGet("whois")]
    [ProducesResponseType(typeof(WhoisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WhoisDto>> GetWhoisAsync(
        [FromQuery] string name,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<WhoisDto>(new GetWhoisQuery(name), ct);
        return Ok(result);
    }

    /// <summary>Registers a new domain name for a client.</summary>
    /// <param name="req">Registration details including domain name, years, and registrant info.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with a link to the new domain on success.</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterAsync(ApiRegisterDomainRequest req, CancellationToken ct)
    {
        var domainId = await bus.InvokeAsync<int>(
            new RegisterDomainCommand(
                req.ClientId,
                req.Name,
                req.Years,
                req.EnableWhoisPrivacy,
                req.AutoRenew,
                null,
                null),
            ct);
        return Created($"/api/domains/{domainId}", null);
    }

    /// <summary>Initiates an incoming domain transfer for a client.</summary>
    /// <param name="req">Transfer details including domain name, EPP code, and registrant info.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with a link to the new domain on success.</returns>
    [HttpPost("transfer")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TransferAsync(ApiTransferDomainRequest req, CancellationToken ct)
    {
        var domainId = await bus.InvokeAsync<int>(
            new TransferDomainCommand(req.ClientId, req.Name, req.EppCode, req.EnableWhoisPrivacy),
            ct);
        return Created($"/api/domains/{domainId}", null);
    }

    /// <summary>Renews a domain registration for additional years.</summary>
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
        await bus.InvokeAsync(new RenewDomainCommand(id, req.Years), ct);
        return NoContent();
    }

    /// <summary>Enables or disables automatic renewal for a domain.</summary>
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

    /// <summary>Enables or disables WHOIS privacy for a domain.</summary>
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

    /// <summary>Enables or disables the registrar transfer-lock for a domain.</summary>
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
        await bus.InvokeAsync(new SetRegistrarLockCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Replaces the nameserver list for a domain.</summary>
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
        await bus.InvokeAsync(new UpdateNameserversCommand(id, req.Nameservers), ct);
        return NoContent();
    }

    /// <summary>Adds a new DNS record to a domain's zone.</summary>
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
        await bus.InvokeAsync(
            new AddDnsRecordCommand(id, req.Type, req.Host, req.Value, req.Ttl, req.Priority),
            ct);
        return Created($"/api/domains/{id}", null);
    }

    /// <summary>Updates an existing DNS record in a domain's zone.</summary>
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
        await bus.InvokeAsync(
            new UpdateDnsRecordCommand(id, recordId, req.Value, req.Ttl, req.Priority),
            ct);
        return NoContent();
    }

    /// <summary>Deletes a DNS record from a domain's zone.</summary>
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
        await bus.InvokeAsync(new DeleteDnsRecordCommand(id, recordId), ct);
        return NoContent();
    }

    /// <summary>Cancels a pending incoming domain transfer.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/cancel-transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelTransferAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new CancelTransferCommand(id), ct);
        return NoContent();
    }

    /// <summary>Initiates an outgoing domain transfer and retrieves the EPP code.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The EPP authorization code to hand to the gaining registrar.</returns>
    [HttpPost("{id:int}/initiate-outgoing-transfer")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string>> InitiateOutgoingTransferAsync(int id, CancellationToken ct)
    {
        var eppCode = await bus.InvokeAsync<string>(new InitiateOutgoingTransferCommand(id), ct);
        return Ok(new { eppCode });
    }

    /// <summary>Updates editable domain fields.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Updated domain field values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(int id, UpdateDomainRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateDomainCommand(
            id, req.FirstPaymentAmount, req.RecurringAmount, req.PaymentMethod,
            req.PromotionCode, req.SubscriptionId, req.AdminNotes,
            req.ExpiresAt, req.NextDueDate, req.RegistrationPeriod, req.Status,
            req.Nameservers), ct);
        return NoContent();
    }

    /// <summary>Modifies WHOIS registrant contact details at the registrar.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Updated registrant contact details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPost("{id:int}/modify-contact")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModifyContactAsync(int id, ModifyContactRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new ModifyDomainContactCommand(id, new DomainContact(
            req.FirstName, req.LastName, req.Organization, req.Email, req.Phone,
            req.Address1, req.Address2, req.City, req.State, req.PostalCode, req.Country)), ct);
        return NoContent();
    }

    /// <summary>Toggles DNS management for a domain.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/dns-management")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDnsManagementAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetDnsManagementCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Toggles email forwarding for a domain.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Boolean flag payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/email-forwarding-toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEmailForwardingAsync(int id, SetBoolRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new SetEmailForwardingCommand(id, req.Enabled), ct);
        return NoContent();
    }

    /// <summary>Adds an email forwarding rule.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="req">Email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created on success.</returns>
    [HttpPost("{id:int}/email-forwarding")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddEmailForwardingRuleAsync(int id, EmailForwardingRuleRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new AddEmailForwardingRuleCommand(id, req.Source, req.Destination), ct);
        return Created($"/api/domains/{id}", null);
    }

    /// <summary>Updates an email forwarding rule.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="req">Updated email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/email-forwarding/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEmailForwardingRuleAsync(int id, int ruleId, EmailForwardingRuleRequest req, CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateEmailForwardingRuleCommand(id, ruleId, req.Source, req.Destination, req.IsActive), ct);
        return NoContent();
    }

    /// <summary>Deletes an email forwarding rule.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}/email-forwarding/{ruleId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmailForwardingRuleAsync(int id, int ruleId, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteEmailForwardingRuleCommand(id, ruleId), ct);
        return NoContent();
    }
}
