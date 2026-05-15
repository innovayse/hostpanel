namespace Innovayse.Application.Clients.Queries.GetMyProfile;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.DTOs;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetMyProfileQuery"/>.
/// Finds the client record linked to the authenticated user and returns their profile.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="userService">User service for email lookup.</param>
public sealed class GetMyProfileHandler(IClientRepository clientRepo, IUserService userService)
{
    /// <summary>
    /// Retrieves the client profile for the authenticated user.
    /// </summary>
    /// <param name="query">The query containing the user's Identity ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's full profile DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client record exists for the user.</exception>
    public async Task<ClientDto> HandleAsync(GetMyProfileQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByUserIdAsync(query.UserId, ct)
            ?? throw new InvalidOperationException($"No client profile found for user {query.UserId}.");

        var user = await userService.FindByIdAsync(client.UserId, ct);
        var email = user?.Email ?? "";

        return MapToDto(client, email);
    }

    /// <summary>Maps a <see cref="Client"/> aggregate to <see cref="ClientDto"/>.</summary>
    /// <param name="client">The client aggregate to map.</param>
    /// <param name="email">The email from the Identity user.</param>
    /// <returns>The mapped DTO.</returns>
    private static ClientDto MapToDto(Client client, string email) =>
        new(
            client.Id,
            client.UserId,
            email,
            client.FirstName,
            client.LastName,
            client.CompanyName,
            client.Phone,
            client.Status,
            client.Street,
            client.Address2,
            client.City,
            client.State,
            client.PostCode,
            client.Country,
            client.Currency,
            client.PaymentMethod,
            client.BillingContact,
            client.AdminNotes,
            client.NotifyGeneral,
            client.NotifyInvoice,
            client.NotifySupport,
            client.NotifyProduct,
            client.NotifyDomain,
            client.NotifyAffiliate,
            client.LateFees,
            client.OverdueNotices,
            client.TaxExempt,
            client.SeparateInvoices,
            client.DisableCcProcessing,
            client.MarketingOptIn,
            client.StatusUpdate,
            client.AllowSso,
            client.CreatedAt,
            client.Contacts.Select(c => new ContactDto(
                c.Id, c.FirstName, c.LastName, c.CompanyName,
                c.Email, c.Phone, c.Type,
                c.Street, c.Address2, c.City, c.State, c.PostCode, c.Country,
                c.NotifyGeneral, c.NotifyInvoice, c.NotifySupport,
                c.NotifyProduct, c.NotifyDomain, c.NotifyAffiliate)).ToList());
}
