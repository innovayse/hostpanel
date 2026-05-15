namespace Innovayse.API.Clients;

using System.Security.Claims;
using Innovayse.API.Clients.Requests;
using Innovayse.Application.Clients.Commands.UpdateClient;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client self-service portal endpoints.
/// Authenticated clients can view and update their own profile.
/// The client's Identity user ID is extracted from the JWT sub claim.
/// </summary>
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
        var userId = GetUserId();
        var result = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
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
        var userId = GetUserId();

        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);

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

        var updated = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(userId), ct);
        return Ok(updated);
    }

    /// <summary>Extracts the authenticated user's Identity ID from JWT claims.</summary>
    /// <returns>The user ID string.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user ID claim is missing.</exception>
    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID not found in token.");
}
