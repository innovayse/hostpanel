namespace Innovayse.Application.Clients.Commands.AdminCreateClient;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="AdminCreateClientCommand"/>.
/// Creates a new <see cref="Client"/> aggregate, optionally provisioning
/// a new Identity user or linking an existing one.
/// </summary>
/// <param name="userService">Identity user management service.</param>
/// <param name="clientRepo">Client persistence repository.</param>
/// <param name="uow">Unit of work for transactional persistence.</param>
public sealed class AdminCreateClientHandler(IUserService userService, IClientRepository clientRepo, IUnitOfWork uow)
{
    /// <summary>
    /// Processes the admin client creation command.
    /// Creates or resolves the Identity user, builds the <see cref="Client"/> aggregate
    /// with all profile, address, notification, and billing settings, then persists.
    /// </summary>
    /// <param name="cmd">The admin create client command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created client.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the existing user ID is not found or already has a linked client.
    /// </exception>
    public async Task<int> HandleAsync(AdminCreateClientCommand cmd, CancellationToken ct)
    {
        string userId;
        string email;

        if (cmd.CreateNewUser)
        {
            userId = await userService.CreateAsync(cmd.Email!, cmd.Password!, ct);
            await userService.AddToRoleAsync(userId, Roles.Client, ct);

            if (cmd.Language is not null)
            {
                await userService.UpdateUserAsync(userId, cmd.FirstName, cmd.LastName, cmd.Email!, cmd.Language, ct);
            }

            email = cmd.Email!;
        }
        else
        {
            var existingUser = await userService.FindByIdAsync(cmd.ExistingUserId!, ct)
                ?? throw new InvalidOperationException($"User with ID '{cmd.ExistingUserId}' was not found.");

            var existingClient = await clientRepo.FindByUserIdAsync(cmd.ExistingUserId!, ct);
            if (existingClient is not null)
            {
                throw new InvalidOperationException(
                    $"User '{cmd.ExistingUserId}' already has a linked client account (Client ID: {existingClient.Id}).");
            }

            userId = existingUser.Id;
            email = cmd.Email ?? existingUser.Email;
        }

        var status = ParseStatus(cmd.Status);

        var client = Client.Create(userId, cmd.FirstName, cmd.LastName, email, cmd.CompanyName);

        client.Update(cmd.FirstName, cmd.LastName, cmd.CompanyName, cmd.Phone);

        client.UpdateAddress(cmd.Street, cmd.Address2, cmd.City, cmd.State, cmd.PostCode, cmd.Country);

        client.UpdatePreferences(cmd.Currency, cmd.PaymentMethod, cmd.BillingContact, cmd.AdminNotes);

        client.UpdateNotifications(
            cmd.NotifyGeneral, cmd.NotifyInvoice, cmd.NotifySupport,
            cmd.NotifyProduct, cmd.NotifyDomain, cmd.NotifyAffiliate);

        client.UpdateSettings(
            cmd.LateFees, cmd.OverdueNotices, cmd.TaxExempt, cmd.SeparateInvoices,
            cmd.DisableCcProcessing, cmd.MarketingOptIn, cmd.StatusUpdate, cmd.AllowSso);

        // Apply non-Active status if specified (Client.Create defaults to Active).
        ApplyStatus(client, status);

        clientRepo.Add(client);
        await uow.SaveChangesAsync(ct);

        return client.Id;
    }

    /// <summary>
    /// Parses a status string to <see cref="ClientStatus"/>, defaulting to <see cref="ClientStatus.Active"/>.
    /// </summary>
    /// <param name="status">The status string to parse, or <see langword="null"/>.</param>
    /// <returns>The parsed <see cref="ClientStatus"/> value.</returns>
    private static ClientStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ClientStatus.Active;
        }

        return Enum.TryParse<ClientStatus>(status, ignoreCase: true, out var parsed)
            ? parsed
            : ClientStatus.Active;
    }

    /// <summary>
    /// Applies the desired status to the client aggregate using domain methods.
    /// Skips if the status is already <see cref="ClientStatus.Active"/> (the default).
    /// </summary>
    /// <param name="client">The client aggregate to update.</param>
    /// <param name="status">The desired status.</param>
    private static void ApplyStatus(Client client, ClientStatus status)
    {
        switch (status)
        {
            case ClientStatus.Active:
                break; // Already active after Create.
            case ClientStatus.Inactive:
                // No domain method for Inactive — set via Suspend then Activate pattern is not applicable.
                // The domain currently only supports Active, Suspended, Closed transitions.
                // For now, we leave as Active if Inactive is requested.
                break;
            case ClientStatus.Suspended:
                client.Suspend();
                break;
            case ClientStatus.Closed:
                client.Suspend();
                client.Close();
                break;
        }
    }
}
