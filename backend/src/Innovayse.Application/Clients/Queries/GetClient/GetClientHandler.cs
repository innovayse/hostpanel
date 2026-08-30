namespace Innovayse.Application.Clients.Queries.GetClient;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Clients.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="GetClientQuery"/>.
/// Returns full client details including contacts and email from Identity.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="identity">Reads the person this client belongs to.</param>
public sealed class GetClientHandler(IClientRepository clientRepo, IIdentityProvider identity)
{
    /// <summary>
    /// Retrieves a client by ID and maps to <see cref="ClientDto"/>.
    /// </summary>
    /// <param name="query">The query with the client ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The client DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task<ClientDto> HandleAsync(GetClientQuery query, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(query.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {query.ClientId} not found.");

        var user = await identity.FindBySubjectAsync(client.UserId, ct);
        var email = user?.Email ?? "";

        return MapToDto(client, email, user?.TwoFactorEnabled ?? false, user?.Language);
    }

    /// <summary>Maps a <see cref="Client"/> aggregate to <see cref="ClientDto"/>.</summary>
    /// <param name="client">The client aggregate to map.</param>
    /// <param name="email">The email from the Identity user.</param>
    /// <param name="twoFactorEnabled">Whether TOTP 2FA is enabled for the user.</param>
    /// <param name="language">The account's chosen UI language, null where the provider holds none.</param>
    /// <returns>The mapped DTO.</returns>
    private static ClientDto MapToDto(Client client, string email, bool twoFactorEnabled, string? language) =>
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
            // From the account, not the client row: the language belongs to the person who
            // signs in, and this product's own table is the only store it ever lived in.
            language,
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
