namespace Innovayse.API.Email;

using Innovayse.Application.Email.Commands.CreateAlias;
using Innovayse.Application.Email.Commands.CreateEmailDomain;
using Innovayse.Application.Email.Commands.CreateMailbox;
using Innovayse.Application.Email.Commands.DeleteAlias;
using Innovayse.Application.Email.Commands.DeleteMailbox;
using Innovayse.Application.Email.Commands.UpdateMailboxPassword;
using Innovayse.Application.Email.Commands.VerifyEmailDomainDns;
using Innovayse.Application.Email.DTOs;
using Innovayse.Application.Email.Queries.GetEmailDomain;
using Innovayse.Application.Email.Queries.GetRequiredDnsRecords;
using Innovayse.Application.Email.Queries.ListAliases;
using Innovayse.Application.Email.Queries.ListEmailDomains;
using Innovayse.Application.Email.Queries.ListMailboxes;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Wolverine;

/// <summary>
/// Client-facing endpoints for managing the authenticated client's own email domains,
/// mailboxes, and aliases. Requires the Client role.
/// </summary>
/// <param name="bus">Wolverine message bus for dispatching commands and queries.</param>
[ApiController]
[Route("api/my-email")]
[Authorize(Roles = Roles.Client)]
public sealed class MyEmailController(IMessageBus bus) : ControllerBase
{
    // ── Email Domains ────────────────────────────────────────────────────────

    /// <summary>Returns all email domains belonging to the authenticated client.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of email domain DTOs for the authenticated client.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmailDomainDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EmailDomainDto>>> ListDomainsAsync(CancellationToken ct)
    {
        var clientId = await GetClientIdAsync(ct);
        var result = await bus.InvokeAsync<IReadOnlyList<EmailDomainDto>>(
            new ListEmailDomainsQuery(clientId), ct);
        return Ok(result);
    }

    /// <summary>Returns a single email domain by its primary key.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The email domain DTO, or 404 if not found.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmailDomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailDomainDto>> GetDomainAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<EmailDomainDto?>(
            new GetEmailDomainQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Provisions a new email domain for the authenticated client.</summary>
    /// <param name="req">Domain creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created email domain DTO.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(EmailDomainDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EmailDomainDto>> CreateDomainAsync(
        [FromBody] CreateEmailDomainRequest req,
        CancellationToken ct)
    {
        var clientId = await GetClientIdAsync(ct);
        var result = await bus.InvokeAsync<EmailDomainDto>(
            new CreateEmailDomainCommand(clientId, req.Domain, req.MaxMailboxes, req.MaxAliases, req.MaxQuotaMb),
            ct);
        return Ok(result);
    }

    // ── DNS ──────────────────────────────────────────────────────────────────

    /// <summary>Returns the required DNS records for an email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of required DNS record DTOs.</returns>
    [HttpGet("{id:int}/dns-records")]
    [ProducesResponseType(typeof(IReadOnlyList<DnsRecordRequirementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DnsRecordRequirementDto>>> GetDnsRecordsAsync(
        int id,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<DnsRecordRequirementDto>>(
            new GetRequiredDnsRecordsQuery(id), ct);
        return Ok(result);
    }

    /// <summary>
    /// Triggers live DNS verification for an email domain.
    /// Automatically activates the domain if all required records pass.
    /// </summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of DNS record verification results.</returns>
    [HttpPost("{id:int}/verify-dns")]
    [ProducesResponseType(typeof(IReadOnlyList<DnsRecordRequirementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DnsRecordRequirementDto>>> VerifyDnsAsync(
        int id,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<DnsRecordRequirementDto>>(
            new VerifyEmailDomainDnsCommand(id), ct);
        return Ok(result);
    }

    // ── Mailboxes ────────────────────────────────────────────────────────────

    /// <summary>Returns all mailboxes belonging to the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of mailbox DTOs.</returns>
    [HttpGet("{id:int}/mailboxes")]
    [ProducesResponseType(typeof(IReadOnlyList<MailboxDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MailboxDto>>> ListMailboxesAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<MailboxDto>>(
            new ListMailboxesQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Creates a new mailbox on the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="req">Mailbox creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created mailbox DTO.</returns>
    [HttpPost("{id:int}/mailboxes")]
    [ProducesResponseType(typeof(MailboxDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MailboxDto>> CreateMailboxAsync(
        int id,
        [FromBody] CreateMailboxRequest req,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<MailboxDto>(
            new CreateMailboxCommand(id, req.LocalPart, req.DisplayName, req.Password, req.QuotaMb),
            ct);
        return Ok(result);
    }

    /// <summary>Deletes a mailbox from the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="mailboxId">Mailbox primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}/mailboxes/{mailboxId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMailboxAsync(int id, int mailboxId, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteMailboxCommand(id, mailboxId), ct);
        return NoContent();
    }

    /// <summary>Updates the password for a mailbox on the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="mailboxId">Mailbox primary key.</param>
    /// <param name="req">New password request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}/mailboxes/{mailboxId:int}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePasswordAsync(
        int id,
        int mailboxId,
        [FromBody] UpdatePasswordRequest req,
        CancellationToken ct)
    {
        await bus.InvokeAsync(new UpdateMailboxPasswordCommand(id, mailboxId, req.NewPassword), ct);
        return NoContent();
    }

    // ── Aliases ──────────────────────────────────────────────────────────────

    /// <summary>Returns all mail aliases belonging to the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of mail alias DTOs.</returns>
    [HttpGet("{id:int}/aliases")]
    [ProducesResponseType(typeof(IReadOnlyList<MailAliasDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MailAliasDto>>> ListAliasesAsync(int id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<MailAliasDto>>(
            new ListAliasesQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Creates a new mail alias on the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="req">Alias creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created mail alias DTO.</returns>
    [HttpPost("{id:int}/aliases")]
    [ProducesResponseType(typeof(MailAliasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MailAliasDto>> CreateAliasAsync(
        int id,
        [FromBody] CreateAliasRequest req,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<MailAliasDto>(
            new CreateAliasCommand(id, req.SourceAddress, req.DestinationAddress),
            ct);
        return Ok(result);
    }

    /// <summary>Deletes a mail alias from the specified email domain.</summary>
    /// <param name="id">Email domain primary key.</param>
    /// <param name="aliasId">Mail alias primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}/aliases/{aliasId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAliasAsync(int id, int aliasId, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteAliasCommand(id, aliasId), ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token.");

    private async Task<int> GetClientIdAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        var profile = await bus.InvokeAsync<Application.Clients.DTOs.ClientDto>(
            new GetMyProfileQuery(userId), ct);
        return profile.Id;
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────

    /// <summary>Request body for creating a new email domain.</summary>
    public record CreateEmailDomainRequest(
        string Domain,
        int MaxMailboxes = 10,
        int MaxAliases = 50,
        int MaxQuotaMb = 5120);

    /// <summary>Request body for creating a new mailbox.</summary>
    public record CreateMailboxRequest(
        string LocalPart,
        string DisplayName,
        string Password,
        int QuotaMb = 512);

    /// <summary>Request body for updating a mailbox password.</summary>
    public record UpdatePasswordRequest(string NewPassword);

    /// <summary>Request body for creating a new mail alias.</summary>
    public record CreateAliasRequest(string SourceAddress, string DestinationAddress);
}
