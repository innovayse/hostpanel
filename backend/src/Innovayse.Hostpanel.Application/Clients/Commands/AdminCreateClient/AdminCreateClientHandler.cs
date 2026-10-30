using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.AdminCreateClient;

/// <summary>
/// Creates a new <see cref="Client"/> aggregate, optionally provisioning a new account or linking
/// an existing one.
/// </summary>
public sealed class AdminCreateClientHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the account when linking to an existing one.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Creates the account when asked to make a new one.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>Grants the new owner the <see cref="Role.Client"/> role.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>Wolverine bus, used to ask the Billing module whether a currency is offered.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="identity">Reads the account when linking to an existing one.</param>
    /// <param name="provisioning">Creates the account when asked to make a new one.</param>
    /// <param name="roles">Grants the new owner the <see cref="Role.Client"/> role.</param>
    /// <param name="bus">Wolverine bus, used to ask the Billing module whether a currency is offered.</param>
    /// <param name="clock">Reads the current moment.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    public AdminCreateClientHandler(
        IHostpanelDbContext context,
        IIdentityProvider identity,
        IUserProvisioning provisioning,
        ISubjectRoleStore roles,
        IMessageBus bus,
        IClock clock,
        IActivityLogger activity)
    {
        _activity = activity;
        _context = context;
        _identity = identity;
        _provisioning = provisioning;
        _roles = roles;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>
    /// Processes the admin client creation command. Creates or resolves the Identity user, builds
    /// the <see cref="Client"/> aggregate with all profile, address, notification and billing
    /// settings, then persists.
    /// </summary>
    /// <remarks>
    /// Every check that can refuse (the currency, an existing account and whether it
    /// already owns a client) runs before an account is created, so a refusal never leaves one
    /// behind. If saving the client then fails, the account this call created is removed again and
    /// the failure is rethrown; the removal is a real purge (see <see cref="IUserProvisioning.PurgeUnusedAsync"/>),
    /// not the soft delete, so the address is free for the admin to try again.
    /// </remarks>
    /// <param name="cmd">The admin create client command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new client's id, or <c>IDENTITY_USER_NOT_FOUND</c> when the existing user id names
    /// nobody, <c>EXISTING_USER_ALREADY_HAS_CLIENT</c> when that user already owns a client,
    /// <c>CURRENCY_NOT_FOUND</c> when the requested currency is not configured and enabled here,
    /// <c>ACCOUNT_EMAIL_EXISTS</c> when the new account's address is taken, or
    /// <c>USER_PROVISIONING_NOT_ALLOWED_CREATE_ACCOUNT</c> when asked to create a new account where
    /// an external sign-on provider owns the accounts.
    /// </returns>
    public async Task<Result<int>> HandleAsync(AdminCreateClientCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // A currency is chosen once. Left blank, the client has none recorded and is billed in the
        // base by the resolver; given, it must be one the panel actually offers, because this row
        // is what every later invoice is raised in.
        var currencyCode = string.IsNullOrWhiteSpace(cmd.Preferences.Currency) ? null : cmd.Preferences.Currency.ToUpperInvariant();
        if (currencyCode is not null && !await _bus.IsCurrencyOfferedAsync(currencyCode, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        }

        string userId;
        string email;
        var createdAccount = false;

        if (cmd.CreateNewUser)
        {
            // The name goes in at creation rather than in a second write afterwards, so an admin
            // who left the language field alone still gets a client whose name is on the account.
            var created = await _provisioning.CreateAsync(
                new CreateUserInput(cmd.Email!, cmd.Password!, cmd.FirstName, cmd.LastName), ct);
            if (created.IsFailure)
            {
                return Result<int>.FailureCode(created.ErrorCode!);
            }

            userId = created.Value!;
            email = cmd.Email!;
            createdAccount = true;
        }
        else
        {
            var existingUser = await _identity.FindBySubjectAsync(cmd.ExistingUserId!, ct);
            if (existingUser is null)
            {
                return Result<int>.FailureCode(HostpanelErrorCodes.IDENTITY_USER_NOT_FOUND);
            }

            var alreadyHasClient = await _context.Clients
                .AsNoTracking()
                .AnyAsync(c => c.UserId == cmd.ExistingUserId, ct);
            if (alreadyHasClient)
            {
                return Result<int>.FailureCode(HostpanelErrorCodes.EXISTING_USER_ALREADY_HAS_CLIENT);
            }

            userId = existingUser.Subject;
            email = cmd.Email ?? existingUser.Email;
        }

        int clientId;
        try
        {
            if (createdAccount)
            {
                await _roles.AddAsync(userId, Role.Client, ct);

                if (cmd.Preferences.Language is not null)
                {
                    await _provisioning.UpdateProfileAsync(
                        new UserProfileUpdate(userId, cmd.FirstName, cmd.LastName, cmd.Preferences.Language), ct);
                }
            }

            clientId = await SaveClientAsync(cmd, userId, email, cmd.Status ?? ClientStatus.Active, currencyCode, ct);

            // An existing account linked as owner holds no Client role unless it once registered or
            // ordered here, and without it the portal answers 403 to its own client.
            if (!createdAccount)
            {
                await _roles.AddAsync(userId, Role.Client, ct);
            }
        }
        catch (Exception) when (createdAccount)
        {
            // Only an account this call created is this call's to remove; a linked one existed before.
            await RemoveCreatedAccountAsync(userId);
            throw;
        }

        // Saved after the client, as every other line that names a database-assigned id is. The
        // client and its account are consistent by now, so a failure here leaves both in place.
        await _activity.RecordAsync(new ActivityLogEntry(clientId, $"Created Client - Client ID: {clientId}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result<int>.Success(clientId);
    }

    /// <summary>Builds the client from the command and saves it.</summary>
    /// <param name="cmd">The admin create client command.</param>
    /// <param name="userId">The account that owns the client.</param>
    /// <param name="email">The owner's address, stamped on the creation event.</param>
    /// <param name="status">The status to start the client in.</param>
    /// <param name="currencyCode">The upper-case currency code already checked to be on offer, or <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new client's id.</returns>
    private async Task<int> SaveClientAsync(
        AdminCreateClientCommand cmd, string userId, string email, ClientStatus status, string? currencyCode, CancellationToken ct)
    {
        var client = Client.Create(userId, cmd.FirstName, cmd.LastName, email, _clock.UtcNow, cmd.CompanyName);

        client.Update(cmd.FirstName, cmd.LastName, cmd.CompanyName, cmd.Phone);
        client.UpdateAddress(
            cmd.Address.Street, cmd.Address.Address2, cmd.Address.City, cmd.Address.State, cmd.Address.PostCode, cmd.Address.Country);
        client.UpdatePreferences(cmd.Preferences.PaymentMethod, cmd.Preferences.BillingContact, cmd.Preferences.AdminNotes);

        if (currencyCode is not null)
        {
            client.SetCurrency(currencyCode);
        }

        // A group the body leaves out keeps the defaults Client.Create starts with.
        if (cmd.Notifications is { } notifications)
        {
            client.UpdateNotifications(
                notifications.General, notifications.Invoice, notifications.Support,
                notifications.Product, notifications.Domain, notifications.Affiliate);
        }

        if (cmd.Settings is { } settings)
        {
            client.UpdateSettings(
                settings.OverdueNotices, settings.TaxExempt, settings.SeparateInvoices,
                settings.DisableCcProcessing, settings.MarketingOptIn, settings.StatusUpdate, settings.AllowSso);
        }

        // Applies a non-Active status if specified (Client.Create defaults to Active); the matrix of allowed moves lives in Client.
        ApplyStatus(client, status, _clock.UtcNow);

        _context.Clients.Add(client);
        await _context.SaveChangesAsync(ct);

        return client.Id;
    }

    /// <summary>Removes the account and its client role after the client could not be saved.</summary>
    /// <param name="userId">The account this call created.</param>
    /// <returns>A task that completes once the account is removed.</returns>
    private async Task RemoveCreatedAccountAsync(string userId)
    {
        // The failed save left its entities tracked, and the removal below saves through the same
        // context: without this it would retry them and fail the same way. Not cancelled with the
        // request either: an abandoned request must still clean up after itself.
        _context.DiscardPendingChanges();
        await _roles.RemoveAsync(userId, Role.Client, CancellationToken.None);
        await _provisioning.PurgeUnusedAsync(userId, CancellationToken.None);
    }

    /// <summary>
    /// Applies the desired status to the client aggregate using domain methods. Skips if the
    /// status is already <see cref="ClientStatus.Active"/> (the default).
    /// </summary>
    /// <param name="client">The client aggregate to update.</param>
    /// <param name="status">The desired status.</param>
    /// <param name="now">The current moment (UTC), stamped on the status change.</param>
    private static void ApplyStatus(Client client, ClientStatus status, DateTimeOffset now)
    {
        switch (status)
        {
            case ClientStatus.Active:
                break;
            case ClientStatus.Inactive:
                client.Deactivate(now);
                break;
            case ClientStatus.Suspended:
                client.Suspend(now);
                break;
            case ClientStatus.Closed:
                client.Close(now);
                break;
        }
    }
}
