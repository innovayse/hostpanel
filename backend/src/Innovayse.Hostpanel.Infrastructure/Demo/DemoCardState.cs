using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// What visitors changed on the demo's sample payment methods (which one each customer made the default
/// and which were removed), the payment intents the demo gateway opened and the saved-method charges it took. One instance lives for the whole process (the card gateway is scoped, the
/// changes must outlive a request) and the demo reset clears it together with the database.
/// </summary>
public sealed class DemoCardState
{
    /// <summary>The default method each customer chose, by customer id.</summary>
    private readonly ConcurrentDictionary<string, string> _chosenDefaults = new();

    /// <summary>The ids of the methods that were removed; every id carries its customer.</summary>
    private readonly ConcurrentDictionary<string, byte> _removed = new();

    /// <summary>The payment intents the demo gateway opened, by intent id.</summary>
    private readonly ConcurrentDictionary<string, CardPaymentIntentStatus> _intents = new();

    /// <summary>The saved-method charges the demo gateway took, by idempotency key.</summary>
    private readonly ConcurrentDictionary<string, CardChargeLookupResult> _charges = new();

    /// <summary>The cards visitors added beside the three sample methods, by customer id.</summary>
    private readonly ConcurrentDictionary<string, List<CardPaymentMethodDto>> _added = new();

    /// <summary>The setup intents the demo gateway opened, by setup intent id.</summary>
    private readonly ConcurrentDictionary<string, CardSetupIntentStatus> _setupIntents = new();

    /// <summary>Remembers a card a visitor added.</summary>
    /// <param name="customerId">The customer the card was saved against.</param>
    /// <param name="method">The saved card, in the shape a real gateway reports it.</param>
    public void AddMethod(string customerId, CardPaymentMethodDto method)
    {
        var list = _added.GetOrAdd(customerId, _ => []);
        lock (list)
        {
            list.Add(method);
        }
    }

    /// <summary>Reads the cards visitors added for a customer.</summary>
    /// <param name="customerId">The customer.</param>
    /// <returns>The added cards, oldest first; empty when none.</returns>
    public IReadOnlyList<CardPaymentMethodDto> AddedMethods(string customerId)
    {
        if (!_added.TryGetValue(customerId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return [.. list];
        }
    }

    /// <summary>Remembers a setup intent the demo gateway opened.</summary>
    /// <param name="setupIntentId">The setup intent id.</param>
    /// <param name="status">What the setup intent saved, reported as succeeded.</param>
    public void OpenSetupIntent(string setupIntentId, CardSetupIntentStatus status) => _setupIntents[setupIntentId] = status;

    /// <summary>Reads back a setup intent the demo gateway opened.</summary>
    /// <param name="setupIntentId">The setup intent id.</param>
    /// <returns>The setup intent, or <see langword="null"/> when the demo never opened it.</returns>
    public CardSetupIntentStatus? FindSetupIntent(string setupIntentId) =>
        _setupIntents.TryGetValue(setupIntentId, out var status) ? status : null;

    /// <summary>Remembers a saved-method charge the demo gateway took.</summary>
    /// <param name="idempotencyKey">The key the charge was sent with.</param>
    /// <param name="charge">What the charge took.</param>
    public void RecordCharge(string idempotencyKey, CardChargeLookupResult charge) => _charges[idempotencyKey] = charge;

    /// <summary>Reads back a saved-method charge the demo gateway took.</summary>
    /// <param name="idempotencyKey">The key the charge was sent with.</param>
    /// <returns>The charge, or <see langword="null"/> when the demo took none under the key.</returns>
    public CardChargeLookupResult? FindCharge(string idempotencyKey) =>
        _charges.TryGetValue(idempotencyKey, out var charge) ? charge : null;

    /// <summary>Remembers a payment intent the demo gateway opened.</summary>
    /// <param name="intentId">The intent id.</param>
    /// <param name="status">What the intent was opened for, reported as settled.</param>
    public void OpenIntent(string intentId, CardPaymentIntentStatus status) => _intents[intentId] = status;

    /// <summary>Reads back a payment intent the demo gateway opened.</summary>
    /// <param name="intentId">The intent id.</param>
    /// <returns>The intent, or <see langword="null"/> when the demo never opened it.</returns>
    public CardPaymentIntentStatus? FindIntent(string intentId) =>
        _intents.TryGetValue(intentId, out var status) ? status : null;

    /// <summary>Remembers the default a customer chose.</summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="methodId">The method made the default.</param>
    public void ChooseDefault(string customerId, string methodId) => _chosenDefaults[customerId] = methodId;

    /// <summary>Remembers that a method was removed.</summary>
    /// <param name="methodId">The removed method.</param>
    public void Remove(string methodId) => _removed[methodId] = 0;

    /// <summary>Whether a method was removed.</summary>
    /// <param name="methodId">The method.</param>
    /// <returns><see langword="true"/> when it was removed.</returns>
    public bool IsRemoved(string methodId) => _removed.ContainsKey(methodId);

    /// <summary>Reads the default a customer chose.</summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="methodId">The chosen method when there is one.</param>
    /// <returns><see langword="true"/> when the customer chose a default.</returns>
    public bool TryGetDefault(string customerId, out string methodId)
    {
        var found = _chosenDefaults.TryGetValue(customerId, out var chosen);
        methodId = chosen ?? string.Empty;
        return found;
    }

    /// <summary>Forgets every change, so the sample methods read as they did when first seeded.</summary>
    public void Clear()
    {
        _chosenDefaults.Clear();
        _removed.Clear();
        _intents.Clear();
        _charges.Clear();
        _added.Clear();
        _setupIntents.Clear();
    }
}
