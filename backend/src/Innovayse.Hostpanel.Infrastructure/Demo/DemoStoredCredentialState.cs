using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>
/// In-memory book of the demo gateway's stored credentials: the methods each customer saved, the open save
/// sessions and every charge by its idempotency key. One instance lives for the whole process, because the
/// demo payment plugin is built again on every resolve and its charges must outlive it.
/// </summary>
public sealed class DemoStoredCredentialState
{
    /// <summary>Guards every collection below; the demo is low traffic, so one lock keeps the invariants simple.</summary>
    private readonly object _gate = new();

    /// <summary>Save sessions that were opened and not yet completed, by session id, holding the customer they belong to.</summary>
    private readonly Dictionary<string, string> _sessions = new(StringComparer.Ordinal);

    /// <summary>Stored methods by customer reference, in the order they were saved.</summary>
    private readonly Dictionary<string, List<SavedMethod>> _methods = new(StringComparer.Ordinal);

    /// <summary>Charges by customer reference and idempotency key.</summary>
    private readonly Dictionary<(string Customer, string Key), StoredChargeResult> _charges = new();

    /// <summary>The source of the current moment, used for the stored card's expiry year.</summary>
    private readonly IClock _clock;

    /// <summary>Source of the numbers that make session ids unique.</summary>
    private int _sessionCounter;

    /// <summary>Creates an empty book.</summary>
    /// <param name="clock">The source of the current moment.</param>
    public DemoStoredCredentialState(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>Gets how many charges actually took effect, that is, how many keys were first seen; a repeated key does not add to it.</summary>
    public int ChargeCount
    {
        get
        {
            lock (_gate)
            {
                return _charges.Count;
            }
        }
    }

    /// <summary>Opens a save session for a customer.</summary>
    /// <param name="customerRef">The customer the method will belong to.</param>
    /// <returns>The new session id.</returns>
    public string OpenSession(string customerRef)
    {
        lock (_gate)
        {
            var sessionId = $"demo-save-{++_sessionCounter}";
            _sessions[sessionId] = customerRef;
            return sessionId;
        }
    }

    /// <summary>Finishes a save session by storing a demo card for its customer.</summary>
    /// <param name="sessionId">The session id from <see cref="OpenSession"/>.</param>
    /// <returns>The stored method, or <see langword="null"/> when the session is unknown or was already completed.</returns>
    public SavedMethod? CompleteSession(string sessionId)
    {
        lock (_gate)
        {
            if (!_sessions.Remove(sessionId, out var customerRef))
            {
                return null;
            }

            if (!_methods.TryGetValue(customerRef, out var methods))
            {
                methods = [];
                _methods[customerRef] = methods;
            }

            var method = new SavedMethod($"demo-pm-{sessionId}", "visa", "4242", 12, _clock.UtcNow.Year + 3, methods.Count == 0);
            methods.Add(method);
            return method;
        }
    }

    /// <summary>Lists a customer's stored methods.</summary>
    /// <param name="customerRef">The customer reference.</param>
    /// <returns>A snapshot of the methods in the order they were saved.</returns>
    public IReadOnlyList<SavedMethod> Methods(string customerRef)
    {
        lock (_gate)
        {
            return _methods.TryGetValue(customerRef, out var methods) ? [.. methods] : [];
        }
    }

    /// <summary>Makes one stored method the default and clears the flag on the others.</summary>
    /// <param name="customerRef">The customer reference.</param>
    /// <param name="methodRef">The method to make the default.</param>
    public void SetDefault(string customerRef, string methodRef)
    {
        lock (_gate)
        {
            if (_methods.TryGetValue(customerRef, out var methods) && methods.Any(m => m.MethodRef == methodRef))
            {
                for (var i = 0; i < methods.Count; i++)
                {
                    methods[i] = methods[i] with { IsDefault = methods[i].MethodRef == methodRef };
                }
            }
        }
    }

    /// <summary>Removes a stored method; when it was the default the first remaining one becomes the default.</summary>
    /// <param name="customerRef">The customer reference.</param>
    /// <param name="methodRef">The method to remove.</param>
    public void Remove(string customerRef, string methodRef)
    {
        lock (_gate)
        {
            if (!_methods.TryGetValue(customerRef, out var methods))
            {
                return;
            }

            var removed = methods.RemoveAll(m => m.MethodRef == methodRef);
            if (removed > 0 && methods.Count > 0 && !methods.Any(m => m.IsDefault))
            {
                methods[0] = methods[0] with { IsDefault = true };
            }
        }
    }

    /// <summary>Records the result of a charge unless the key already has one; the first result for a key is final.</summary>
    /// <param name="customerRef">The customer reference.</param>
    /// <param name="idempotencyKey">The attempt's idempotency key.</param>
    /// <param name="result">The result to record for a key seen for the first time.</param>
    /// <returns>The result recorded for the key: <paramref name="result"/> on the first call, the earlier result on a repeat.</returns>
    public StoredChargeResult Record(string customerRef, string idempotencyKey, StoredChargeResult result)
    {
        lock (_gate)
        {
            if (_charges.TryGetValue((customerRef, idempotencyKey), out var existing))
            {
                return existing;
            }

            _charges[(customerRef, idempotencyKey)] = result;
            return result;
        }
    }

    /// <summary>Finds the charge made with a key.</summary>
    /// <param name="customerRef">The customer reference.</param>
    /// <param name="idempotencyKey">The attempt's idempotency key.</param>
    /// <returns>The recorded result, or <see langword="null"/> when the key never charged.</returns>
    public StoredChargeResult? Find(string customerRef, string idempotencyKey)
    {
        lock (_gate)
        {
            return _charges.TryGetValue((customerRef, idempotencyKey), out var result) ? result : null;
        }
    }

    /// <summary>Forgets every stored method, open save session and charge; the demo reset calls it so the book starts empty again.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _sessions.Clear();
            _methods.Clear();
            _charges.Clear();
            _sessionCounter = 0;
        }
    }
}
