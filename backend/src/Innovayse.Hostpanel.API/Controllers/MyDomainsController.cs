using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Application.Domains.Commands.AddMyDomainDnsRecord;
using Innovayse.Hostpanel.Application.Domains.Commands.AddMyDomainEmailForwardingRule;
using Innovayse.Hostpanel.Application.Domains.Commands.DeleteMyDomainDnsRecord;
using Innovayse.Hostpanel.Application.Domains.Commands.DeleteMyDomainEmailForwardingRule;
using Innovayse.Hostpanel.Application.Domains.Commands.InitiateMyOutgoingTransfer;
using Innovayse.Hostpanel.Application.Domains.Commands.ModifyMyDomainContact;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewMyDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainAutoRenew;
using Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainDnsManagement;
using Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainEmailForwarding;
using Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainRegistrarLock;
using Innovayse.Hostpanel.Application.Domains.Commands.SetMyDomainWhoisPrivacy;
using Innovayse.Hostpanel.Application.Domains.Commands.TransferMyDomain;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateMyDomainDnsRecord;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateMyDomainEmailForwardingRule;
using Innovayse.Hostpanel.Application.Domains.Commands.UpdateMyDomainNameservers;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomain;
using Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomainContact;
using Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomainNameservers;
using Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomains;
using Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomainWhois;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client-facing endpoints for managing the authenticated client's own domains.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </summary>
/// <remarks>
/// <para>
/// No action here reads a claim and none checks ownership. The rule that a domain must belong to
/// the caller lives in the client-facing <c>My*</c> handlers, which resolve the caller themselves
/// and answer a <see cref="Result"/> rather than throwing. It belongs there because every command
/// these actions dispatch, <c>SetAutoRenewCommand</c>, <c>AddDnsRecordCommand</c> and the rest,
/// is also dispatched by the admin <c>DomainsController</c>/<c>DomainDnsController</c>, so a check
/// at this endpoint would guarantee nothing about the message itself.
/// </para>
/// <para>
/// A domain belonging to somebody else, a domain that does not exist, and a caller with no client
/// record all answer 404 with <c>DOMAIN_NOT_FOUND</c>. Domain ids are sequential, and telling those
/// three apart is itself a way of enumerating them.
/// </para>
/// </remarks>
[Route("api/me/domains")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyDomainsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyDomainsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns all domains belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the list of domain DTOs; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller
    /// has no client record; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewDomains</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewDomains)]
    [ProducesResponseType(typeof(IReadOnlyList<MyDomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyDomainsAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<MyDomainDto>>>(new GetMyDomainsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single domain belonging to the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the domain DTO; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's; 401 without a session;
    /// 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewDomains</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewDomains)]
    [ProducesResponseType(typeof(MyDomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyDomainDto>>(new GetMyDomainQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Enables or disables automatic renewal for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">The desired auto-renew flag value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/auto-renew")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetAutoRenewAsync(
        int id, [FromBody] SetMyDomainAutoRenewCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Enables or disables WHOIS privacy for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">The desired WHOIS privacy flag value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there or there is no privacy subscription; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/whois-privacy")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetWhoisPrivacyAsync(
        int id, [FromBody] SetMyDomainWhoisPrivacyCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Enables or disables the registrar transfer-lock for a domain owned by the authenticated
    /// client.
    /// </summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Whether the domain should be locked.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/lock")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetRegistrarLockAsync(
        int id, [FromBody] SetMyDomainRegistrarLockCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns the nameserver list for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the ordered nameserver list; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the
    /// caller's; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewDomains</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/nameservers")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewDomains)]
    [ProducesResponseType(typeof(IReadOnlyList<NameserverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNameserversAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<NameserverDto>>>(
            new GetMyDomainNameserversQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Replaces the nameserver list for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">New nameserver list (minimum 2 entries).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 when fewer than 2 nameservers are given; 404 with
    /// <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502 when the registrar rejects
    /// the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/nameservers")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateNameserversAsync(
        int id, [FromBody] UpdateMyDomainNameserversCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Performs a WHOIS lookup for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the WHOIS record; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the
    /// caller's, or <c>DOMAIN_WHOIS_NOT_FOUND</c> when the registrar has no record for the name;
    /// 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewDomains</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/whois")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewDomains)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(WhoisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWhoisAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<WhoisDto>>(new GetMyDomainWhoisQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Reads the current WHOIS registrant contact of a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the contact; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's,
    /// or <c>DOMAIN_CONTACT_NOT_FOUND</c> when the registrar holds no contact for it; 401 without a session; 403 for
    /// a caller who is not a client; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewDomains</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/contact")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewDomains)]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(DomainContactDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContactAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<DomainContactDto>>(new GetMyDomainContactQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Modifies WHOIS registrant contact details for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Updated registrant contact details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/whois")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ModifyWhoisContactAsync(
        int id, [FromBody] ModifyMyDomainContactCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Retrieves the EPP authorization code for an outgoing transfer of a domain owned by the
    /// authenticated client.
    /// </summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the EPP authorization code; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the
    /// caller's; 400 when the domain carries no registrar reference; 502 when the registrar
    /// rejects the request or returns no code; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPost("{id:int}/epp")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(typeof(EppCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetEppCodeAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<EppCodeDto>>(new InitiateMyOutgoingTransferCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Places an order to transfer a domain in to the authenticated client's own account.
    /// </summary>
    /// <remarks>
    /// The command binds straight off the body and carries no client id, so there is nothing on
    /// this route to check: the account the transfer lands on comes from the credential, inside
    /// the handler. Deliberately not routed at the admin <c>POST /api/domains/transfer</c>, whose
    /// command takes its client from the request body: a client-facing endpoint reaching an
    /// admin use case that trusts a body-supplied id is the exact shape of this repository's
    /// last IDOR.
    /// </remarks>
    /// <param name="command">Domain name, EPP code, period and the gateway to bill through.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new order and the invoice raised for it: the registrar is not called until
    /// that invoice is paid; 400 when the payment method or currency is not on offer; 404 with
    /// <c>PRODUCT_NOT_FOUND</c> when an ordered product does not exist; 503 with
    /// <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c> when this installation sells no domain product; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>PlaceNewOrders</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("transfer-order")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.PlaceNewOrders)]
    [ProducesResponseType(typeof(PlaceOrderResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TransferInAsync([FromBody] TransferMyDomainCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PlaceOrderResultDto>>(command, ct);
        return HandleResult(outcome, order => CreatedResult($"/api/orders/{order.OrderId}", order));
    }

    /// <summary>
    /// Places an order to renew a domain registration on behalf of the authenticated client.
    /// </summary>
    /// <remarks>
    /// The domain id comes off the route, never the body, and the account it must belong to
    /// comes from the credential inside the handler. Deliberately not routed at the admin
    /// <c>POST /api/domains/{id}/renew</c>, which calls the registrar at once and raises no
    /// invoice: correct where a person has already decided the renewal is warranted, and a way
    /// of giving paid renewals away where the customer drives it.
    /// </remarks>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Renewal period and the gateway to bill through.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new order and the invoice raised for it: the registrar is not called until
    /// that invoice is paid; 400 when the payment method or currency is not on offer; 404 with
    /// <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's, or <c>PRODUCT_NOT_FOUND</c> when
    /// an ordered product does not exist; 409 with <c>RENEWAL_INVOICE_OPEN</c> when an open renewal
    /// invoice for this domain has to be settled first; 503 with <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c> when this installation sells no domain
    /// product; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>PlaceNewOrders</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/renew")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.PlaceNewOrders)]
    [ProducesResponseType(typeof(PlaceOrderResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RenewAsync(
        int id, [FromBody] RenewMyDomainCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PlaceOrderResultDto>>(command with { DomainId = id }, ct);
        return HandleResult(outcome, order => CreatedResult($"/api/orders/{order.OrderId}", order));
    }

    /// <summary>Adds a new DNS record to a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with a link to the domain; 400 when a field is refused; 404 with
    /// <c>DOMAIN_NOT_FOUND</c> when it is not the caller's; 502 when the registrar rejects the
    /// addition; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPost("{id:int}/dns")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddDnsRecordAsync(
        int id, [FromBody] AddMyDomainDnsRecordCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return outcome.IsFailure ? MapFailure(outcome) : CreatedResult($"/api/me/domains/{id}", (object?)null);
    }

    /// <summary>Updates an existing DNS record in a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="recordId">DNS record primary key; wins over any id in the body.</param>
    /// <param name="command">Updated DNS record details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's, or
    /// <c>DNS_RECORD_NOT_FOUND</c> when the record id names no record on that domain; 502 when the
    /// registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/dns/{recordId:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateDnsRecordAsync(
        int id, int recordId, [FromBody] UpdateMyDomainDnsRecordCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id, RecordId = recordId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a DNS record from a domain's zone on behalf of the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="recordId">DNS record primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's, or
    /// <c>DNS_RECORD_NOT_FOUND</c> when the record id names no record on that domain; 502 when the
    /// registrar rejects the deletion; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpDelete("{id:int}/dns/{recordId:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteDnsRecordAsync(int id, int recordId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteMyDomainDnsRecordCommand(id, recordId), ct);
        return HandleResult(outcome);
    }

    /// <summary>Toggles DNS management for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Whether DNS management should be enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/dns-management")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetDnsManagementAsync(
        int id, [FromBody] SetMyDomainDnsManagementCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Toggles email forwarding for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Whether email forwarding should be enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when the domain is not the caller's; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/email-forwarding-toggle")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetEmailForwardingAsync(
        int id, [FromBody] SetMyDomainEmailForwardingCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an email forwarding rule to a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="command">Email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 on success; 400 when a field is refused; 404 with <c>DOMAIN_NOT_FOUND</c> when the
    /// domain is not the caller's; 502 when the registrar rejects the addition; 401 without a session; 403 for a
    /// caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPost("{id:int}/email-forwarding")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddEmailForwardingRuleAsync(
        int id, [FromBody] AddMyDomainEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id }, ct);
        return outcome.IsFailure ? MapFailure(outcome) : CreatedResult($"/api/me/domains/{id}", (object?)null);
    }

    /// <summary>Updates an email forwarding rule for a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key; wins over any id in the body.</param>
    /// <param name="ruleId">Email forwarding rule primary key; wins over any id in the body.</param>
    /// <param name="command">Updated email forwarding rule details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's, or
    /// <c>EMAIL_FORWARDING_RULE_NOT_FOUND</c> when the rule id names no rule on that domain; 502
    /// when the registrar rejects the update; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 400 when the body, query or route fails validation; 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpPut("{id:int}/email-forwarding/{ruleId:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateEmailForwardingRuleAsync(
        int id, int ruleId, [FromBody] UpdateMyDomainEmailForwardingRuleCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { DomainId = id, RuleId = ruleId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes an email forwarding rule from a domain owned by the authenticated client.</summary>
    /// <param name="id">Domain primary key.</param>
    /// <param name="ruleId">Email forwarding rule primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>DOMAIN_NOT_FOUND</c> when it is not the caller's, or
    /// <c>EMAIL_FORWARDING_RULE_NOT_FOUND</c> when the rule id names no rule on that domain; 502
    /// when the registrar rejects the deletion; 401 without a session; 403 for a caller who is not a client;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ManageDomainSettings</c>;
    /// 409 with <c>REGISTRAR_OPERATION_UNSUPPORTED</c> or <c>REGISTRAR_DOMAIN_NOT_ACTIVE</c> when the registrar does not offer the operation or the domain is not active there; 429 when the rate limit is hit; 503 with <c>REGISTRAR_NOT_CONFIGURED</c> when no registrar is set up.
    /// </returns>
    [HttpDelete("{id:int}/email-forwarding/{ruleId:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ManageDomainSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteEmailForwardingRuleAsync(int id, int ruleId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteMyDomainEmailForwardingRuleCommand(id, ruleId), ct);
        return HandleResult(outcome);
    }
}
