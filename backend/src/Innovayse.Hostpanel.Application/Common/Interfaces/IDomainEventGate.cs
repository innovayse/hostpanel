namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Lets a bulk, system-driven write say "these changes are not news": while a scope is suppressed,
/// the events entities raise are discarded at save time instead of being published.
/// </summary>
/// <remarks>
/// A migration pull creates thousands of clients, invoices and tickets that already exist in the
/// source system, and a dev seed does the same on a throwaway database. Publishing their creation
/// and payment events would e-mail every migrated client a welcome and a payment receipt. The gate
/// is scoped, so suppression lasts exactly as long as the unit of work that asked for it.
/// </remarks>
public interface IDomainEventGate
{
    /// <summary>Whether events raised by saves in this scope are currently being discarded.</summary>
    public bool IsSuppressed { get; }

    /// <summary>Starts discarding events for this scope until the returned handle is disposed.</summary>
    /// <returns>A handle that restores the previous state when disposed.</returns>
    public IDisposable Suppress();
}
