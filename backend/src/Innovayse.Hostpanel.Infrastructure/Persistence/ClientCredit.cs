using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IClientCredit"/>: changes the tracked client through its own
/// credit methods on the scoped context the calling handler saves, and writes the movement to the
/// client ledger in the same unit of work so the credit history cannot miss one.
/// </summary>
public sealed class ClientCredit : IClientCredit
{
    /// <summary>The longest description the ledger holds.</summary>
    private const int MaxDescriptionLength = 500;

    /// <summary>The application DbContext, the same scoped instance the calling handler saves.</summary>
    private readonly AppDbContext _db;

    /// <summary>The clock the history line's moment is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="ClientCredit"/> class.</summary>
    /// <param name="db">The application DbContext, the same scoped instance the calling handler saves.</param>
    /// <param name="clock">The clock the history line's moment is read from.</param>
    public ClientCredit(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<Result<ClientCreditBalance>> AddAsync(ClientCreditChange change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change);

        var client = await FindAsync(change.ClientId, ct);
        if (client is null)
        {
            return Result<ClientCreditBalance>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        client.AddCredit(change.Amount);
        var entry = await RecordAsync(client, change, change.Amount, ct);
        return Result<ClientCreditBalance>.Success(new ClientCreditBalance(client.Id, client.CreditBalance, entry));
    }

    /// <inheritdoc/>
    public async Task<Result<ClientCreditBalance>> DeductAsync(ClientCreditChange change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change);

        var client = await FindAsync(change.ClientId, ct);
        if (client is null)
        {
            return Result<ClientCreditBalance>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        if (change.Amount > client.CreditBalance)
        {
            return Result<ClientCreditBalance>.FailureCode(HostpanelErrorCodes.CLIENT_CREDIT_INSUFFICIENT);
        }

        client.DeductCredit(change.Amount);
        var entry = await RecordAsync(client, change, -change.Amount, ct);
        return Result<ClientCreditBalance>.Success(new ClientCreditBalance(client.Id, client.CreditBalance, entry));
    }

    /// <inheritdoc/>
    public async Task<Result<ClientCreditBalance>> AddAndSaveAsync(ClientCreditChange change, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Entry is not null)
        {
            // A stamped entry carries the balance of the first attempt and cannot be restamped on the retry below.
            throw new ArgumentException("A change that saves on its own writes its own history line.", nameof(change));
        }

        var added = await AddAsync(change, ct);
        if (added.IsFailure)
        {
            return added;
        }

        var client = (await FindAsync(change.ClientId, ct))!;
        var line = _db.ChangeTracker.Entries<Transaction>()
            .Where(e => e.State == EntityState.Added && e.Entity.CreditMovement == change.Movement && e.Entity.ClientId == client.Id)
            .Select(e => e.Entity)
            .Last();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException ex)
            when (ex.InnerException is DbUpdateConcurrencyException inner
                && inner.Entries.All(e => ReferenceEquals(e.Entity, client) || ReferenceEquals(e.Entity, line)))
        {
            // Only the client row moved underneath: reload it and add once more on the fresh balance. The failed
            // save wrote nothing and the caller's other changes stay staged; the history line carried the stale
            // balance, so it is dropped and written again with the fresh one.
            _db.Entry(line).State = EntityState.Detached;
            await _db.Entry(client).ReloadAsync(ct);
            client.AddCredit(change.Amount);
            await RecordAsync(client, change, change.Amount, ct);
            await _db.SaveChangesAsync(ct);
        }

        return Result<ClientCreditBalance>.Success(new ClientCreditBalance(client.Id, client.CreditBalance));
    }

    /// <summary>
    /// Writes the movement to the ledger: stamps the entry that already holds the money, or adds a credit-only one.
    /// </summary>
    /// <param name="client">The client, with the change already applied to the balance.</param>
    /// <param name="change">What was asked.</param>
    /// <param name="signedAmount">The change with its sign: positive when credit was added.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entry that records the movement.</returns>
    private async Task<Transaction> RecordAsync(Client client, ClientCreditChange change, decimal signedAmount, CancellationToken ct)
    {
        if (change.Entry is { } entry)
        {
            entry.RecordCreditMovement(change.Movement, signedAmount, client.CreditBalance);
            return entry;
        }

        var currency = await _db.GetCreditCurrencyAsync(client, ct);
        var line = Transaction.CreateCreditMovement(
            client.Id, _clock.UtcNow, Describe(change), change.InvoiceId, currency, change.Movement, signedAmount, client.CreditBalance);
        _db.Transactions.Add(line);
        return line;
    }

    /// <summary>The ledger description of a credit-only movement: the caller's reason, or the standard wording.</summary>
    /// <param name="change">What was asked.</param>
    /// <returns>The description, never longer than the ledger holds.</returns>
    private static string Describe(ClientCreditChange change)
    {
        // An invoice not saved yet has no id to name; the caller links the line once it has one.
        var invoice = change.InvoiceId is { } id ? $"Invoice #{id}" : "an invoice";
        var text = !string.IsNullOrWhiteSpace(change.Note)
            ? change.Note.Trim()
            : change.Movement switch
            {
                CreditMovementType.AdminAdd => "Credit added by staff",
                CreditMovementType.AdminDeduct => "Credit taken by staff",
                CreditMovementType.InvoiceApplied => $"Credit applied to {invoice}",
                CreditMovementType.InvoiceReturned => $"Credit returned from {invoice}",
                CreditMovementType.RenewalAutoApplied => $"Credit applied to renewal {invoice}",
                CreditMovementType.RefundToCredit => $"Refund of {invoice} credited to balance",
                CreditMovementType.TopUp => $"Top-up {invoice} paid",
                CreditMovementType.TopUpRefunded => $"Top-up {invoice} refunded",
                CreditMovementType.PaymentSurplus => $"Payment beyond {invoice} credited to balance",
                CreditMovementType.TransactionReversed => "Ledger entry deleted, credit movement undone",
                _ => change.Movement.ToString(),
            };
        return text.Length <= MaxDescriptionLength ? text : text[..MaxDescriptionLength];
    }

    /// <summary>Finds the client, reusing the instance the context already tracks.</summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked client, or null when the id names none.</returns>
    private async Task<Client?> FindAsync(int clientId, CancellationToken ct)
    {
        return await _db.Clients.FindAsync([clientId], ct);
    }
}
