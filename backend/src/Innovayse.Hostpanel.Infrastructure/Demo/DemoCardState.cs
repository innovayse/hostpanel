using System.Collections.Concurrent;
using Innovayse.Hostpanel.Application.Billing.Common;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// In-memory memory of the demo card gateway: the payment intents it opened, so verifying one reports what it was
/// opened for. One instance lives for the whole process and is emptied by the demo reset.
/// </summary>
public sealed class DemoCardState
{
    /// <summary>The intents the demo opened, by id.</summary>
    private readonly ConcurrentDictionary<string, CardPaymentIntentStatus> _intents = new();

    /// <summary>Remembers a payment intent the demo opened.</summary>
    /// <param name="intentId">The intent id.</param>
    /// <param name="status">What it was opened for and that it settled.</param>
    public void OpenIntent(string intentId, CardPaymentIntentStatus status) => _intents[intentId] = status;

    /// <summary>Finds an intent the demo opened.</summary>
    /// <param name="intentId">The intent id.</param>
    /// <returns>What it was opened for, or <see langword="null"/> when the demo never opened it.</returns>
    public CardPaymentIntentStatus? FindIntent(string intentId) =>
        _intents.TryGetValue(intentId, out var status) ? status : null;

    /// <summary>Forgets everything; called by the demo reset.</summary>
    public void Clear() => _intents.Clear();
}
