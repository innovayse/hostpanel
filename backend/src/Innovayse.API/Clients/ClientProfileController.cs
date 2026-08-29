namespace Innovayse.API.Clients;

using Innovayse.API.Clients.Requests;
using Innovayse.Application.Clients.Commands.AddContact;
using Innovayse.Application.Clients.Commands.RemoveContact;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Clients.Commands.UpdateContact;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client self-service portal endpoints.
/// Authenticated clients can view and update their own profile.
/// </summary>
/// <remarks>
/// Which account is never in the route or the body, and is not read from a claim here either:
/// <see cref="GetMyProfileQuery"/> resolves the caller inside its own handler, and every write
/// below is scoped to the id that query answers with.
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/clients/me")]
[Authorize(Roles = Roles.Client)]
public sealed class ClientProfileController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns the authenticated client's own profile.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's full profile including contacts and billing address.</returns>
    [HttpGet]
    public async Task<ActionResult<ClientDto>> GetMyProfileAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the authenticated client's own profile and billing address.
    /// The ClientId is resolved from the JWT — clients cannot update other profiles.
    /// </summary>
    /// <param name="request">Updated profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated client profile.</returns>
    [HttpPut]
    public async Task<ActionResult<ClientDto>> UpdateMyProfileAsync(
        [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        await bus.InvokeAsync(
            new UpdateClientCommand(
                profile.Id,
                request.Email,
                request.FirstName,
                request.LastName,
                request.CompanyName,
                request.Phone,
                request.Street,
                request.Address2,
                request.City,
                request.State,
                request.PostCode,
                request.Country,
                request.Currency,
                request.PaymentMethod,
                request.BillingContact,
                request.AdminNotes,
                request.NotifyGeneral,
                request.NotifyInvoice,
                request.NotifySupport,
                request.NotifyProduct,
                request.NotifyDomain,
                request.NotifyAffiliate,
                request.LateFees,
                request.OverdueNotices,
                request.TaxExempt,
                request.SeparateInvoices,
                request.DisableCcProcessing,
                request.MarketingOptIn,
                request.StatusUpdate,
                request.AllowSso,
                request.Status),
            ct);

        var updated = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);
        return Ok(updated);
    }

    /// <summary>
    /// Returns the additional contacts on the authenticated client's own account.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account's contacts, empty when it has none.</returns>
    /// <remarks>
    /// Read from the profile rather than through a query of its own, because the profile
    /// already carries them — <see cref="ClientDto.Contacts"/>. The client portal calls this
    /// as a list of its own, and it answered 404 until now, so the account area's Contacts tab
    /// had nothing to render.
    /// </remarks>
    [HttpGet("contacts")]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> GetMyContactsAsync(CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);
        return Ok(profile.Contacts);
    }

    /// <summary>
    /// Adds a contact to the authenticated client's own account.
    /// </summary>
    /// <param name="request">Contact data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    /// <remarks>
    /// The client id comes from the profile the credential resolves to, never from the request:
    /// that is what stops one account adding a contact — and with it a notification address — to
    /// somebody else's.
    /// </remarks>
    [HttpPost("contacts")]
    public async Task<IActionResult> AddMyContactAsync(
        [FromBody] AddContactRequest request, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        await bus.InvokeAsync(
            new AddContactCommand(
                profile.Id, request.FirstName, request.LastName, request.CompanyName,
                request.Email, request.Phone, request.Type,
                request.Street, request.Address2, request.City, request.State,
                request.PostCode, request.Country,
                request.NotifyGeneral, request.NotifyInvoice, request.NotifySupport,
                request.NotifyProduct, request.NotifyDomain, request.NotifyAffiliate),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Updates a contact on the authenticated client's own account.
    /// </summary>
    /// <param name="contactId">The contact primary key.</param>
    /// <param name="request">Updated contact data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpPut("contacts/{contactId:int}")]
    public async Task<IActionResult> UpdateMyContactAsync(
        int contactId, [FromBody] UpdateContactRequest request, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        await bus.InvokeAsync(
            new UpdateContactCommand(
                profile.Id, contactId,
                request.FirstName, request.LastName, request.CompanyName,
                request.Email, request.Phone, request.Type,
                request.Street, request.Address2, request.City, request.State,
                request.PostCode, request.Country,
                request.NotifyGeneral, request.NotifyInvoice, request.NotifySupport,
                request.NotifyProduct, request.NotifyDomain, request.NotifyAffiliate),
            ct);

        return NoContent();
    }

    /// <summary>
    /// Removes a contact from the authenticated client's own account.
    /// </summary>
    /// <param name="contactId">The contact primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("contacts/{contactId:int}")]
    public async Task<IActionResult> RemoveMyContactAsync(int contactId, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);
        await bus.InvokeAsync(new RemoveContactCommand(profile.Id, contactId), ct);
        return NoContent();
    }
}
