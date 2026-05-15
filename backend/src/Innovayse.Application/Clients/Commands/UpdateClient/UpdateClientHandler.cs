namespace Innovayse.Application.Clients.Commands.UpdateClient;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="UpdateClientCommand"/>.
/// Loads the client aggregate, applies profile, address, preference, notification,
/// settings, and status changes, then saves. Optionally updates the Identity user's email.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
/// <param name="userService">Identity user service for email updates.</param>
public sealed class UpdateClientHandler(IClientRepository clientRepo, IUnitOfWork uow, IUserService userService)
{
    /// <summary>
    /// Updates the client's profile, billing address, preferences, notifications,
    /// settings, and status.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the client is not found.</exception>
    public async Task HandleAsync(UpdateClientCommand cmd, CancellationToken ct)
    {
        var client = await clientRepo.FindByIdAsync(cmd.ClientId, ct)
            ?? throw new InvalidOperationException($"Client {cmd.ClientId} not found.");

        client.Update(cmd.FirstName, cmd.LastName, cmd.CompanyName, cmd.Phone);
        client.UpdateAddress(cmd.Street, cmd.Address2, cmd.City, cmd.State, cmd.PostCode, cmd.Country);
        client.UpdatePreferences(cmd.Currency, cmd.PaymentMethod, cmd.BillingContact, cmd.AdminNotes);
        client.UpdateNotifications(cmd.NotifyGeneral, cmd.NotifyInvoice, cmd.NotifySupport, cmd.NotifyProduct, cmd.NotifyDomain, cmd.NotifyAffiliate);
        client.UpdateSettings(cmd.LateFees, cmd.OverdueNotices, cmd.TaxExempt, cmd.SeparateInvoices, cmd.DisableCcProcessing, cmd.MarketingOptIn, cmd.StatusUpdate, cmd.AllowSso);

        if (cmd.Status is not null)
        {
            if (!Enum.TryParse<ClientStatus>(cmd.Status, ignoreCase: true, out var newStatus))
                throw new InvalidOperationException($"Invalid client status: '{cmd.Status}'.");

            if (newStatus != client.Status)
            {
                switch (newStatus)
                {
                    case ClientStatus.Active:
                        client.Activate();
                        break;
                    case ClientStatus.Suspended:
                        client.Suspend();
                        break;
                    case ClientStatus.Closed:
                        client.Close();
                        break;
                    case ClientStatus.Inactive:
                        // Inactive is set by Suspend or initial state — no dedicated domain method
                        break;
                }
            }
        }

        // Update Identity user email if provided and the client has a linked user
        if (cmd.Email is not null && client.UserId is not null)
        {
            await userService.UpdateUserAsync(
                client.UserId, cmd.FirstName, cmd.LastName, cmd.Email, null, ct);
        }

        await uow.SaveChangesAsync(ct);
    }
}
