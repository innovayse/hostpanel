using System.Collections.Concurrent;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A scriptable stored credential capability for the integration tests: one gateway module whose customers,
/// saved methods and charges live in memory, with the knobs the money-safety tests need (a charge that throws
/// after the money was taken, a lookup that fails, a charge held at a rendezvous). A refusal is a
/// <see cref="StoredChargeOutcome.Declined"/> outcome and an unknown outcome is an exception, as the contract says.
/// </summary>
public sealed class FakeStoredCredentialPlugin : IStoredCredentialPlugin
{
    /// <summary>Guards the dictionaries below: requests reach the fake concurrently.</summary>
    private readonly object _gate = new();

    /// <summary>The customer reference per client reference, so a repeated ensure answers the same account.</summary>
    private readonly Dictionary<string, string> _customers = [];

    /// <summary>Gets the methods every customer has on file, in the order listed.</summary>
    public List<SavedMethod> Methods { get; } = [];

    /// <summary>Gets or sets what <see cref="CompleteSaveMethodAsync"/> answers.</summary>
    public SavedMethodResult CompletedSession { get; set; } = new(true, new SavedMethod("pm_saved_1", "visa", "4242", 12, 2035, true));

    /// <summary>Gets or sets how a begun session asks the payer to continue.</summary>
    public SaveMethodSession BegunSession { get; set; } = new("seti_test_1", SaveMethodMode.EmbeddedToken, "seti_test_1_secret", null);

    /// <summary>Gets or sets what a first charge for a key answers; a repeated key answers its first result.</summary>
    public Func<StoredChargeInstruction, StoredChargeResult> OnCharge { get; set; } = instruction => new StoredChargeResult(
        StoredChargeOutcome.Succeeded, $"txn-{instruction.IdempotencyKey}", instruction.AmountMinor, instruction.Currency, null);

    /// <summary>Gets or sets whether a charge throws after the money was recorded, as a timeout whose answer was lost does.</summary>
    public bool ThrowsAfterCharging { get; set; }

    /// <summary>Gets or sets whether a charge throws before anything was taken.</summary>
    public bool ThrowsBeforeCharging { get; set; }

    /// <summary>Gets or sets whether a lookup by key throws.</summary>
    public bool LookupThrows { get; set; }

    /// <summary>Gets or sets whether listing, setting the default and removing methods throw.</summary>
    public bool MethodCallsThrow { get; set; }

    /// <summary>Gets or sets whether opening a customer throws.</summary>
    public bool CustomerThrows { get; set; }

    /// <summary>Gets or sets a hold that makes parallel charges meet before any of them answers.</summary>
    public ChargeRendezvous? ChargeRendezvous { get; set; }

    /// <summary>Gets or sets a step run first thing in every charge, to hold a charge while another request runs or to change the database under it.</summary>
    public Func<StoredChargeInstruction, Task>? BeforeCharge { get; set; }

    /// <summary>Gets or sets a step run after a customer was created and before the call answers, to open a race window.</summary>
    public Func<Task>? AfterCustomerCreated { get; set; }

    /// <summary>Gets the charges by idempotency key; the first result for a key is final.</summary>
    public ConcurrentDictionary<string, (StoredChargeInstruction Instruction, StoredChargeResult Result)> Charges { get; } = new();

    /// <summary>Gets how many customers were created.</summary>
    public int CustomersCreated
    {
        get
        {
            lock (_gate)
            {
                return _customers.Count;
            }
        }
    }

    /// <summary>Gets how many lookups by key were made.</summary>
    public int ChargeLookups { get; private set; }

    /// <summary>Gets the last method made the default.</summary>
    public string? LastDefaultMethod { get; private set; }

    /// <summary>Gets the last method removed.</summary>
    public string? LastRemovedMethod { get; private set; }

    /// <summary>Gets the last save session context.</summary>
    public SaveMethodContext? LastSaveContext { get; private set; }

    /// <inheritdoc/>
    public async Task<CredentialCustomerResult> EnsureCustomerAsync(CredentialCustomerContext context, CancellationToken ct)
    {
        if (CustomerThrows)
        {
            throw new TimeoutException("The fake gateway did not answer.");
        }

        string customerRef;
        lock (_gate)
        {
            if (!_customers.TryGetValue(context.ClientReference, out customerRef!))
            {
                customerRef = $"cus_fake_{context.ClientReference}_{Guid.NewGuid():N}"[..28];
                _customers[context.ClientReference] = customerRef;
            }
        }

        if (AfterCustomerCreated is { } after)
        {
            await after();
        }

        return new CredentialCustomerResult(customerRef);
    }

    /// <inheritdoc/>
    public Task<SaveMethodSession> BeginSaveMethodAsync(SaveMethodContext context, CancellationToken ct)
    {
        LastSaveContext = context;
        return Task.FromResult(BegunSession);
    }

    /// <inheritdoc/>
    public Task<SavedMethodResult> CompleteSaveMethodAsync(string sessionId, CancellationToken ct) =>
        Task.FromResult(CompletedSession);

    /// <inheritdoc/>
    public async Task<StoredChargeResult> ChargeAsync(StoredChargeInstruction instruction, CancellationToken ct)
    {
        if (BeforeCharge is { } before)
        {
            await before(instruction);
        }

        if (ChargeRendezvous is { } rendezvous)
        {
            await rendezvous.ArriveAsync(ct);
        }

        if (ThrowsBeforeCharging)
        {
            throw new TimeoutException("The fake gateway did not answer.");
        }

        var recorded = Charges.GetOrAdd(instruction.IdempotencyKey, _ => (instruction, OnCharge(instruction)));
        if (ThrowsAfterCharging)
        {
            throw new TimeoutException("The fake gateway took the money and then did not answer.");
        }

        return recorded.Result;
    }

    /// <inheritdoc/>
    public Task<StoredChargeLookup> FindChargeAsync(string customerRef, string idempotencyKey, CancellationToken ct)
    {
        ChargeLookups++;
        if (LookupThrows)
        {
            throw new TimeoutException("The fake gateway did not answer.");
        }

        return Task.FromResult(Charges.TryGetValue(idempotencyKey, out var recorded)
            ? new StoredChargeLookup(true, recorded.Result.Outcome, recorded.Result.TransactionId, recorded.Result.AmountMinor, recorded.Result.Currency, recorded.Result.ActionUrl)
            : new StoredChargeLookup(false, null, null, null, null, null));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedMethod>> ListMethodsAsync(string customerRef, CancellationToken ct)
    {
        ThrowIfMethodCallsThrow();
        return Task.FromResult<IReadOnlyList<SavedMethod>>([.. Methods]);
    }

    /// <inheritdoc/>
    public Task SetDefaultMethodAsync(string customerRef, string methodRef, CancellationToken ct)
    {
        ThrowIfMethodCallsThrow();
        LastDefaultMethod = methodRef;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveMethodAsync(string customerRef, string methodRef, CancellationToken ct)
    {
        ThrowIfMethodCallsThrow();
        LastRemovedMethod = methodRef;
        Methods.RemoveAll(m => m.MethodRef == methodRef);
        return Task.CompletedTask;
    }

    /// <summary>Throws as a gateway that cannot be reached does, when the method calls are scripted to.</summary>
    /// <exception cref="TimeoutException">When <see cref="MethodCallsThrow"/> is set.</exception>
    private void ThrowIfMethodCallsThrow()
    {
        if (MethodCallsThrow)
        {
            throw new TimeoutException("The fake gateway did not answer.");
        }
    }
}
