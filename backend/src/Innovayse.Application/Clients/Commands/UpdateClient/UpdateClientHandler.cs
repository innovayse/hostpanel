namespace Innovayse.Application.Clients.Commands.UpdateClient;

using Innovayse.Application.Auth.Common;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>
/// Handles <see cref="UpdateClientCommand"/>.
/// Loads the client aggregate, applies profile, address, preference, notification,
/// settings, and status changes, then saves. Optionally updates the account's email.
/// </summary>
/// <param name="clientRepo">Client repository.</param>
/// <param name="uow">Unit of work.</param>
/// <param name="provisioning">
/// Writes the account-side fields -- the sign-in address and the chosen UI language -- where
/// this deployment owns the account.
/// </param>
/// <param name="identity">
/// Reads the address the account currently signs in with, so a save that did not touch it
/// can be told apart from one that did.
/// </param>
public sealed class UpdateClientHandler(
    IClientRepository clientRepo,
    IUnitOfWork uow,
    IUserProvisioning provisioning,
    IIdentityProvider identity)
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
            {
                throw new InvalidOperationException($"Invalid client status: '{cmd.Status}'.");
            }

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

        // Read once and shared by both account-side writes below. The client record holds
        // neither the sign-in address nor the language, so "has this actually changed?" can
        // only be answered against whoever owns the person.
        //
        // `client.UserId` is a non-nullable string that defaults to empty, so a null test on
        // it never excluded anything; blank is what "no linked account" means.
        var account = string.IsNullOrEmpty(client.UserId)
            ? null
            : await identity.FindBySubjectAsync(client.UserId, ct);

        // The account's sign-in address, if this deployment is the one that owns it.
        //
        // Only the address: the names above belong to the client record, which this
        // handler has already updated. The previous version passed them on to the user
        // record as well, so editing a client silently renamed the person behind it —
        // and where an SSO owns that person, renaming them from here is not ours to do.
        //
        // Asked as "has the address changed?", never as "was an address sent?". The account
        // form posts the address on every save because it is a populated field on the form,
        // so the old `is not null` test called through on saves that never touched it -- and
        // where an SSO owns the person that call refuses by design. That is how changing a
        // language, a currency, a phone number or a billing address came back to the customer
        // as a refusal about sign-in addresses.
        //
        // The client record holds no address of its own, so the current one is read from
        // whoever owns the person. Where that lookup finds nobody the call still goes through:
        // a genuine attempt is answered by the provisioner, not silently dropped here.
        if (!string.IsNullOrWhiteSpace(cmd.Email) && !string.IsNullOrEmpty(client.UserId))
        {
            if (account is null
                || !string.Equals(account.Email, cmd.Email, StringComparison.OrdinalIgnoreCase))
            {
                await provisioning.ChangeEmailAsync(client.UserId, cmd.Email, ct);
            }
        }

        // The account holder's chosen UI language.
        //
        // It lives on the account row, not on the client record -- AppUser.Language is the
        // only column this product ever stored it in -- so it is written through the same
        // provisioner as the address, and only where this deployment owns the person.
        //
        // Asked as "has the language changed?", for the same reason the address is: the
        // account form posts it on every save because it is a populated field on the form.
        //
        // The names passed are the account's own, never the command's. Renaming the person
        // behind a client from a client edit is exactly what an earlier version of this
        // handler did by accident; UpdateProfileAsync takes them because the local
        // implementation writes all three at once, and passing back what is already there
        // is how this call sets a language without also being a rename.
        //
        // Null means "leave it alone" and blank means "no preference", which is stored as
        // null: a caller that posts the dropdown's empty option is clearing a choice, not
        // declining to send one, and storing "" would be a language code matching no
        // supported locale that then reads back as English.
        if (cmd.Language is not null && account is not null)
        {
            var language = string.IsNullOrWhiteSpace(cmd.Language) ? null : cmd.Language;

            if (!string.Equals(account.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await provisioning.UpdateProfileAsync(
                        client.UserId, account.FirstName, account.LastName, language, ct);
                }
                catch (UserProvisioningNotAllowedException)
                {
                    // Where an SSO owns the person it owns their language too, and there is
                    // nowhere here to put one. Skipped rather than propagated, and
                    // deliberately unlike the address above: an address change is an intent
                    // the caller has to be told failed, a UI language is a preference the form
                    // posts alongside everything else. Failing the whole save over it is how a
                    // customer editing their phone number gets refused for a field they never
                    // touched.
                    //
                    // Nothing is lost that could have been kept: an SSO-backed provider
                    // answers null for Language, so the portal shows no stored value to
                    // contradict, and the account page does not offer the field in that mode
                    // at all. This catch is for a caller reaching the API directly.
                }
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}
