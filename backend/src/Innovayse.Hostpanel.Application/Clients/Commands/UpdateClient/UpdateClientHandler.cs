using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.ListClientInvoices;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;

/// <summary>
/// Loads the client aggregate, applies profile, address, preference, notification, settings and
/// status changes, then saves. Optionally updates the account's email and language.
/// </summary>
public sealed class UpdateClientHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    /// Writes the account-side fields, the sign-in address and the chosen UI language, where
    /// this deployment owns the account.
    /// </summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>
    /// Reads the address the account currently signs in with, so a save that did not touch it can
    /// be told apart from one that did.
    /// </summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Refuses credential changes on the published demo accounts.</summary>
    private readonly IDemoAccountGuard _demoAccounts;

    /// <summary>Wolverine bus, used to ask the Billing module about the client's invoices and the offered currencies.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock a status change is stamped with.</summary>
    private readonly IClock _clock;

    /// <summary>Records the activity log lines for this action, saved with the change they describe.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Writes the sentence of a currency refusal, which is bound to the currency field of the form.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="provisioning">Writes the account-side fields where this deployment owns the account.</param>
    /// <param name="identity">Reads the address the account currently signs in with.</param>
    /// <param name="demoAccounts">Refuses credential changes on the published demo accounts.</param>
    /// <param name="bus">Wolverine bus, used to ask the Billing module about the client's invoices and the offered currencies.</param>
    /// <param name="clock">The clock a status change is stamped with.</param>
    /// <param name="activity">Records the activity log lines for this action, saved with the change they describe.</param>
    /// <param name="messages">Writes the sentence of a currency refusal, which is bound to the currency field of the form.</param>
    public UpdateClientHandler(
        IHostpanelDbContext context,
        IUserProvisioning provisioning,
        IIdentityProvider identity,
        IDemoAccountGuard demoAccounts,
        IMessageBus bus,
        IClock clock,
        IActivityLogger activity,
        IErrorMessageResolver messages)
    {
        _messages = messages;
        _activity = activity;
        _demoAccounts = demoAccounts;
        _context = context;
        _provisioning = provisioning;
        _identity = identity;
        _bus = bus;
        _clock = clock;
    }

    /// <summary>
    /// Updates the client's profile, billing address, preferences, notifications, settings and status.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CLIENT_NOT_FOUND</c> when the id names no client,
    /// <c>CLIENT_CURRENCY_ALREADY_INVOICED</c> when the currency is changed on a client who has
    /// already been invoiced, <c>CLIENT_CURRENCY_LOCKED_BY_CREDIT</c> when it is changed on a client
    /// who holds a credit balance, <c>CURRENCY_NOT_FOUND</c> when the requested currency is not one
    /// this panel offers, <c>USER_PROVISIONING_NOT_ALLOWED_CHANGE_EMAIL</c> when the
    /// account's sign-in address is changed where an external sign-on provider owns the accounts, or
    /// <c>DEMO_ACTION_DISABLED</c> when the address of a published demo account is changed on a demo deployment, or
    /// <c>EMAIL_ALREADY_IN_USE</c> when another account holds the new address (checked before
    /// anything is saved). The client is saved first and the account's address and language are
    /// written after it; a concurrent edit of the client answers 409 <c>CONFLICT</c> before the
    /// account is touched.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateClientCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var client = await _context.Clients
            .IncludeChildren()
            .FirstOrDefaultAsync(c => c.Id == cmd.ClientId, ct);
        if (client is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        // Read before the writes below overwrite them: the audit lines say whether these changed.
        var notesChanged = !string.Equals(client.AdminNotes, cmd.Preferences.AdminNotes, StringComparison.Ordinal);
        var statusBefore = client.Status;

        client.Update(cmd.FirstName, cmd.LastName, cmd.CompanyName, cmd.Phone);
        client.UpdateAddress(
            cmd.Address.Street, cmd.Address.Address2, cmd.Address.City, cmd.Address.State, cmd.Address.PostCode, cmd.Address.Country);
        client.UpdatePreferences(cmd.Preferences.PaymentMethod, cmd.Preferences.BillingContact, cmd.Preferences.AdminNotes);

        var currencyOutcome = await ApplyCurrencyAsync(client, cmd.Preferences.Currency, ct);
        if (currencyOutcome.IsFailure)
        {
            return currencyOutcome;
        }

        client.UpdateNotifications(
            cmd.Notifications.General, cmd.Notifications.Invoice, cmd.Notifications.Support,
            cmd.Notifications.Product, cmd.Notifications.Domain, cmd.Notifications.Affiliate);
        client.UpdateSettings(
            cmd.Settings.OverdueNotices, cmd.Settings.TaxExempt, cmd.Settings.SeparateInvoices,
            cmd.Settings.DisableCcProcessing, cmd.Settings.MarketingOptIn, cmd.Settings.StatusUpdate, cmd.Settings.AllowSso);

        if (cmd.Status is { } newStatus && newStatus != client.Status)
        {
            var now = _clock.UtcNow;
            switch (newStatus)
            {
                case ClientStatus.Active:
                    client.Activate(now);
                    break;
                case ClientStatus.Suspended:
                    client.Suspend(now);
                    break;
                case ClientStatus.Closed:
                    client.Close(now);
                    break;
                case ClientStatus.Inactive:
                    client.Deactivate(now);
                    break;
            }
        }

        // Read once and shared by both account-side writes below: the client record holds neither
        // the sign-in address nor the language, so "has this actually changed?" can only be
        // answered against whoever owns the person.
        var account = string.IsNullOrEmpty(client.UserId)
            ? null
            : await _identity.FindBySubjectAsync(client.UserId, ct);

        // The account's sign-in address, if this deployment is the one that owns it. Only the
        // address: the names belong to the client record, updated above. Asked as "has the
        // address changed?", never as "was an address sent?": the account form posts it on
        // every save because it is a populated field on the form. Checked now, written after the
        // client is saved: an address another account holds, or a demo account's frozen one, refuses
        // the whole save before anything is written.
        var newEmail = account is not null
            && !string.IsNullOrWhiteSpace(cmd.Email)
            && !string.Equals(account.Email, cmd.Email, StringComparison.OrdinalIgnoreCase)
                ? cmd.Email
                : null;
        if (newEmail is not null)
        {
            // The one rule that keeps the published demo accounts signable: their email is frozen.
            var allowed = await _demoAccounts.EnsureCredentialsUnchangedAsync(client.UserId, ct);
            if (allowed.IsFailure)
            {
                return allowed;
            }

            var holder = await _identity.FindByEmailAsync(newEmail, ct);
            if (holder is not null && holder.Subject != client.UserId)
            {
                return Result.FailureCode(HostpanelErrorCodes.EMAIL_ALREADY_IN_USE);
            }
        }

        // The audit lines name the client and what kind of thing changed, never a value: notes, names
        // and addresses are personal data that do not belong in a log kept for years.
        await _activity.RecordAsync(new ActivityLogEntry(client.Id, $"Modified Client - Client ID: {client.Id}"), ct);
        if (notesChanged)
        {
            await _activity.RecordAsync(new ActivityLogEntry(client.Id, $"Modified Client Notes - Client ID: {client.Id}"), ct);
        }

        if (client.Status != statusBefore)
        {
            await _activity.RecordAsync(
                new ActivityLogEntry(client.Id, $"Changed Client Status - Client ID: {client.Id} - Status: {client.Status}"), ct);
        }

        // The client first, the account second. The account write cannot be rolled back, so it must
        // not run ahead of a save that may still be refused (a concurrent edit answers 409 here).
        await _context.SaveChangesAsync(ct);

        if (newEmail is not null)
        {
            var emailChanged = await _provisioning.ChangeEmailAsync(client.UserId, newEmail, ct);
            if (emailChanged.IsFailure)
            {
                return emailChanged;
            }
        }

        // The account holder's chosen UI language, written through the same provisioner as the
        // address and only where this deployment owns the person. Null means "leave it alone" and
        // blank means "no preference", stored as null.
        if (cmd.Preferences.Language is not null && account is not null)
        {
            var language = string.IsNullOrWhiteSpace(cmd.Preferences.Language) ? null : cmd.Preferences.Language;

            if (!string.Equals(account.Language, language, StringComparison.OrdinalIgnoreCase))
            {
                // Where an external sign-on provider owns the person it owns their language too. A refusal here is
                // ignored rather than propagated: a UI language is a preference the form posts
                // alongside everything else, and failing the whole save over it would refuse a
                // customer editing a field they never touched.
                await _provisioning.UpdateProfileAsync(
                    new UserProfileUpdate(client.UserId, account.FirstName, account.LastName, language), ct);
            }
        }

        return Result.Success();
    }

    /// <summary>Moves the client to the requested currency, when that is still allowed.</summary>
    /// <remarks>
    /// Asked as "has the currency changed?", never as "was a currency sent?": the account form
    /// posts the field on every save, and a client who has been invoiced must still be able to
    /// save their phone number. Changing the code would re-label every existing invoice without
    /// converting an amount, $100 would read as ֏100, so once billed, the currency is fixed.
    /// </remarks>
    /// <param name="client">The client under edit.</param>
    /// <param name="requested">The currency the form posted, or <see langword="null"/> to leave it alone.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>CLIENT_CURRENCY_ALREADY_INVOICED</c> when the client has an invoice that is
    /// not cancelled, <c>CLIENT_CURRENCY_LOCKED_BY_CREDIT</c> when the client holds a credit balance
    /// (the aggregate's rule, see <see cref="Client.SetCurrency"/>), or <c>CURRENCY_NOT_FOUND</c> when the
    /// currency is not configured and enabled. The two lock refusals name the currency field.
    /// </returns>
    private async Task<Result> ApplyCurrencyAsync(Client client, string? requested, CancellationToken ct)
    {
        if (requested is null || string.Equals(requested, client.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        // Only a live invoice locks the currency: a cancelled one charges nothing and keeps its own
        // currency, so a client whose every invoice was cancelled has no billing history to split.
        var invoices = await _bus.InvokeAsync<Result<PaginatedList<InvoiceListItemDto>>>(
            new ListClientInvoicesQuery(client.Id, Page: 1, PageSize: 1), ct);
        var cancelled = await _bus.InvokeAsync<Result<PaginatedList<InvoiceListItemDto>>>(
            new ListClientInvoicesQuery(client.Id, Page: 1, PageSize: 1, Status: InvoiceStatus.Cancelled), ct);
        var alreadyInvoiced = invoices.Value is not { } all || cancelled.Value is not { } gone
            || all.TotalCount > gone.TotalCount;
        if (alreadyInvoiced)
        {
            return CurrencyRefusal(HostpanelErrorCodes.CLIENT_CURRENCY_ALREADY_INVOICED);
        }

        var code = requested.ToUpperInvariant();
        var offered = await _bus.IsCurrencyOfferedAsync(code, ct);
        if (!offered)
        {
            return Result.FailureCode(HostpanelErrorCodes.CURRENCY_NOT_FOUND);
        }

        try
        {
            client.SetCurrency(code);
        }
        catch (DomainRuleException refusal) when (refusal.Code == HostpanelErrorCodes.CLIENT_CURRENCY_LOCKED_BY_CREDIT)
        {
            // The credit rule lives on the aggregate alone; it is turned into a result here so the
            // refusal reaches the form next to the currency field like the invoice lock does.
            return CurrencyRefusal(refusal.Code);
        }

        return Result.Success();
    }

    /// <summary>Builds a refusal that carries its code and the sentence on the currency field.</summary>
    /// <param name="code">The refusal's error code.</param>
    /// <returns>A failed result whose field errors name <c>Preferences.Currency</c>.</returns>
    private Result CurrencyRefusal(string code)
    {
        return Result.Failure(_messages.Resolve(code), code, "Preferences.Currency");
    }
}
