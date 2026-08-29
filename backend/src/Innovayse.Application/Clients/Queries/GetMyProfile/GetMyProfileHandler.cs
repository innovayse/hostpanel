namespace Innovayse.Application.Clients.Queries.GetMyProfile;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetMyProfileQuery"/>.
/// Finds the client record linked to the authenticated user and returns their profile.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="identity">Reads the person this client belongs to.</param>
/// <param name="caller">Whose profile; the query does not say, and must not.</param>
public sealed class GetMyProfileHandler(
    IClientRepository clientRepo,
    IIdentityProvider identity,
    ICurrentRequestContext caller)
{
    /// <summary>
    /// Retrieves the client profile for the authenticated user.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client's full profile DTO.</returns>
    /// <exception cref="ClientProfileNotFoundException">
    /// Thrown when no client record exists for the user. Carries the user id for the log only --
    /// the API answers 404 with a code and an identifier-free sentence.
    /// </exception>
    public async Task<ClientDto> HandleAsync(GetMyProfileQuery query, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clientRepo.FindByUserIdAsync(userId, ct)
            ?? throw new ClientProfileNotFoundException(userId);

        var user = await identity.FindBySubjectAsync(client.UserId, ct);
        var email = user?.Email ?? "";

        return MapToDto(client, email, user?.TwoFactorEnabled ?? false);
    }

    /// <summary>Maps a <see cref="Client"/> aggregate to <see cref="ClientDto"/>.</summary>
    /// <param name="client">The client aggregate to map.</param>
    /// <param name="email">The email from the Identity user.</param>
    /// <param name="twoFactorEnabled">True if the user has TOTP 2FA enabled.</param>
    /// <returns>The mapped DTO.</returns>
    private static ClientDto MapToDto(Client client, string email, bool twoFactorEnabled) =>
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
            twoFactorEnabled,
            client.CreatedAt,
            client.Contacts.Select(c => new ContactDto(
                c.Id, c.FirstName, c.LastName, c.CompanyName,
                c.Email, c.Phone, c.Type,
                c.Street, c.Address2, c.City, c.State, c.PostCode, c.Country,
                c.NotifyGeneral, c.NotifyInvoice, c.NotifySupport,
                c.NotifyProduct, c.NotifyDomain, c.NotifyAffiliate)).ToList());
}
