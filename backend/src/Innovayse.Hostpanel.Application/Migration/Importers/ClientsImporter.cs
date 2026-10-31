using System.Security.Cryptography;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Importers.Enums;
using Innovayse.Hostpanel.Application.Migration.Importers.Records;
using Innovayse.Hostpanel.Application.Migration.Importers.SourceValues;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Migration.Importers;

/// <summary>
/// Imports client records: provisions a new local account for each one not already known, then
/// creates its <see cref="Client"/> row. An account that exists without a client row (an earlier
/// pull that stopped between the two writes) gets its client row on the next pull.
/// </summary>
public sealed class ClientsImporter : IMigrationEntityImporter
{
    /// <summary>How many characters of each class the generated password draws.</summary>
    private const int PasswordClassLength = 12;

    /// <summary>Lowercase letters a generated password draws from.</summary>
    private const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";

    /// <summary>Uppercase letters a generated password draws from.</summary>
    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>Digits a generated password draws from.</summary>
    private const string Digits = "0123456789";

    /// <summary>Symbols a generated password draws from.</summary>
    private const string Symbols = "!#$%&*+-=?@^_";

    /// <summary>Resolves whether a plugin email already has a local account.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Creates the new account for a client this platform has not seen.</summary>
    private readonly IUserProvisioning _provisioning;

    /// <summary>Grants the new account the client role.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Pages through the source plugin's <c>clients</c> dataset and records each record's outcome.</summary>
    private readonly MigrationRecordRunner _runner;

    /// <summary>Reads the moment each client is imported.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="ClientsImporter"/> class.</summary>
    /// <param name="identity">Resolves whether a plugin email already has a local account.</param>
    /// <param name="provisioning">Creates the new account for a client this platform has not seen.</param>
    /// <param name="roles">Grants the new account the client role.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="runner">Pages through the source plugin's <c>clients</c> dataset and records each record's outcome.</param>
    /// <param name="clock">Reads the moment each client is imported.</param>
    public ClientsImporter(
        IIdentityProvider identity,
        IUserProvisioning provisioning,
        ISubjectRoleStore roles,
        IHostpanelDbContext context,
        MigrationRecordRunner runner,
        IClock clock)
    {
        _identity = identity;
        _provisioning = provisioning;
        _roles = roles;
        _context = context;
        _runner = runner;
        _clock = clock;
    }

    /// <inheritdoc/>
    public MigrationEntityType EntityType => MigrationEntityType.Clients;

    /// <inheritdoc/>
    public Task ImportAsync(MigrationJob job, CancellationToken ct) =>
        _runner.RunAsync(new MigrationRecordRun<ClientRecord>(job, EntityType, "clients", ImportClientAsync), ct);

    /// <summary>
    /// Imports one plugin-side client record, provisioning a new local account for it first when
    /// none carries its email yet.
    /// </summary>
    /// <param name="rec">The client record read from the source plugin.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Whether the client was imported or skipped, and why when skipped: it already exists, the
    /// account provider refused to create its account, which this pull cannot work around and
    /// records against the job's own log, or the record has neither a first name nor an email
    /// to take one from.
    /// </returns>
    private async Task<RecordImportResult> ImportClientAsync(ClientRecord rec, CancellationToken ct)
    {
        // A client must have a first name, and a source record can lack one (a company account).
        // Rather than lose the client and everything filed under it, the name falls back to the
        // email's local part, the same default the account store gives an unnamed account; the
        // operator can correct it in the client's profile after the pull. Resolved before any
        // account is created, so a record that cannot be named leaves nothing behind.
        var firstName = string.IsNullOrWhiteSpace(rec.FirstName) ? Client.FirstNameFromEmail(rec.Email) : rec.FirstName;
        if (firstName is null)
        {
            return RecordImportResult.Skipped(MigrationSkipReason.NameMissing);
        }

        string userId;
        var existing = await _identity.FindByEmailAsync(rec.Email, ct);
        if (existing is not null)
        {
            var hasClient = await _context.Clients.AsNoTracking().AnyAsync(c => c.UserId == existing.Subject, ct);
            if (hasClient)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AlreadyExists);
            }

            // The account was created but its client row was not: an earlier pull stopped between
            // the two writes. The client row is created now so the record is not skipped forever.
            userId = existing.Subject;
        }
        else
        {
            // Nobody is told this password: the migrated client signs in for the first time
            // through the password reset flow, which is how they choose a password of their own.
            var created = await _provisioning.CreateAsync(
                new CreateUserInput(rec.Email, GenerateUnusablePassword(), firstName, rec.LastName), ct);
            if (created.IsFailure)
            {
                return RecordImportResult.Skipped(MigrationSkipReason.AccountNotProvisioned);
            }

            userId = created.Value!;
        }

        await _roles.AddAsync(userId, Role.Client, ct);

        var clientEntity = Client.Create(userId, firstName, rec.LastName, rec.Email, _clock.UtcNow, rec.Company);

        clientEntity.Update(firstName, rec.LastName, rec.Company, rec.Phone);

        if (rec.SignupDate is { } signupDate)
        {
            clientEntity.Backdate(signupDate);
        }

        if (!string.IsNullOrWhiteSpace(rec.Address.Street) || !string.IsNullOrWhiteSpace(rec.Address.City))
        {
            clientEntity.UpdateAddress(
                street: rec.Address.Street ?? string.Empty,
                address2: null,
                city: rec.Address.City ?? string.Empty,
                state: rec.Address.State ?? string.Empty,
                postCode: rec.Address.PostCode ?? string.Empty,
                country: rec.Address.Country ?? string.Empty);
        }

        // Recorded as the source had it once trimmed; whether the code is one this panel offers is
        // the operator's to sort out after the pull, the same as every other field imported here.
        // A value that is not a three-letter code is left out rather than failing the client: the
        // currency is optional, and losing the client and everything filed under it for it is worse.
        if (CurrencyCode.TryNormalise(rec.Billing.Currency, out var billingCurrency))
        {
            clientEntity.SetCurrency(billingCurrency);
        }

        if (rec.Billing.TaxExempt)
        {
            // Only the tax flag comes from the source; every other setting keeps its default.
            clientEntity.UpdateSettings(
                overdueNotices: clientEntity.OverdueNotices,
                taxExempt: true,
                separateInvoices: clientEntity.SeparateInvoices,
                disableCcProcessing: clientEntity.DisableCcProcessing,
                marketingOptIn: clientEntity.MarketingOptIn,
                statusUpdate: clientEntity.StatusUpdate,
                allowSso: clientEntity.AllowSso);
        }

        if (rec.Billing.CreditBalance > 0)
        {
            clientEntity.AddCredit(rec.Billing.CreditBalance);
        }

        ApplySourceStatus(clientEntity, rec.Status, _clock.UtcNow);

        _context.Clients.Add(clientEntity);
        await _context.SaveChangesAsync(ct);
        return RecordImportResult.Imported;
    }

    /// <summary>Moves a newly created (active) client to the status it had in the source.</summary>
    /// <param name="client">The client being imported.</param>
    /// <param name="status">The source's status word for the client.</param>
    /// <param name="now">The moment of the import (UTC), stamped on the status change.</param>
    private static void ApplySourceStatus(Client client, string status, DateTimeOffset now)
    {
        switch (status)
        {
            case SourceClientStatuses.Inactive:
                client.Deactivate(now);
                break;
            case SourceClientStatuses.Closed:
                client.Close(now);
                break;
            default:
                // Active, or a word this panel does not know: the client stays active.
                break;
        }
    }

    /// <summary>
    /// Generates a cryptographically random password nobody is told, so the account can only be
    /// signed in to after a password reset. It draws from every character class so it meets any
    /// password policy the account store enforces.
    /// </summary>
    /// <returns>The password.</returns>
    private static string GenerateUnusablePassword() =>
        RandomNumberGenerator.GetString(LowercaseLetters, PasswordClassLength)
        + RandomNumberGenerator.GetString(UppercaseLetters, PasswordClassLength)
        + RandomNumberGenerator.GetString(Digits, PasswordClassLength)
        + RandomNumberGenerator.GetString(Symbols, PasswordClassLength);
}
