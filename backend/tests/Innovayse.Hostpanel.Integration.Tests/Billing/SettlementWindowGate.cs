using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Holds two concurrent card settlements of one charge in the window that used to refund it: both
/// have read the ledger empty, one then records the payment, and only afterwards does the other load
/// the invoice, find it paid and refuse. Two interceptors, <see cref="SettlementWindowLoadInterceptor"/>
/// and <see cref="SettlementWindowCommitInterceptor"/>, drive it; which request is the first to
/// reach the invoice load decides which one records the payment, so the outcome never depends on the
/// order the two requests happen to arrive in.
/// </summary>
/// <remarks>
/// The first settlement to load the invoice waits until the second has reached its own load (so both
/// have read the ledger, before either saved). It then goes on and saves. The second stays held in
/// its load until that save is committed, so it reads the invoice paid. Every later load passes.
/// </remarks>
internal sealed class SettlementWindowGate
{
    /// <summary>The query tag the settlement handler puts on its invoice load, which the gate keys on.</summary>
    public const string InvoiceLoadTag = "settle-invoice-load";

    /// <summary>The longest a request waits for the other before the test fails instead of hanging.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>Guards <see cref="_arrivals"/> and <see cref="_pendingCommits"/>.</summary>
    private readonly object _lock = new();

    /// <summary>The contexts that reached the invoice load, in order; the gate holds only the first two.</summary>
    private readonly List<DbContext> _arrivals = [];

    /// <summary>The contexts whose pending save writes a payment, which releases the held request once it commits.</summary>
    private readonly HashSet<DbContext> _pendingCommits = [];

    /// <summary>Completes when the second request has reached the invoice load.</summary>
    private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the first request's payment is committed.</summary>
    private readonly TaskCompletionSource _committed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Whether the second request was held until the first one's payment committed, which is the
    /// window under test. A test asserts it so a run that never reached the window cannot pass.
    /// </summary>
    public bool WindowExercised { get; private set; }

    /// <summary>
    /// Called before the invoice load runs. The first context holds until the second has arrived; the
    /// second holds until the first's payment is committed; any other load, or a second command of a
    /// context already seen (the load is split into one command per collection), passes.
    /// </summary>
    /// <param name="context">The context loading the invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when this load may run.</returns>
    /// <exception cref="TimeoutException">When the other request never arrives or never commits.</exception>
    public async Task AtInvoiceLoadAsync(DbContext? context, CancellationToken ct)
    {
        int place;
        lock (_lock)
        {
            if (context is null || _arrivals.Count >= 2 || _arrivals.Contains(context))
            {
                return;
            }

            _arrivals.Add(context);
            place = _arrivals.Count;
        }

        if (place == 1)
        {
            await _bothArrived.Task.WaitAsync(Patience, ct);
            return;
        }

        _bothArrived.TrySetResult();
        await _committed.Task.WaitAsync(Patience, ct);
        WindowExercised = true;
    }

    /// <summary>Notes that a context is about to save a payment, so its commit releases the held request.</summary>
    /// <param name="context">The saving context.</param>
    public void SavingPayment(DbContext context)
    {
        lock (_lock)
        {
            if (_arrivals.Count == 2 && _arrivals[0] == context)
            {
                _pendingCommits.Add(context);
            }
        }
    }

    /// <summary>Releases the held request when the context that was saving a payment has committed.</summary>
    /// <param name="context">The context whose save completed.</param>
    public void Saved(DbContext context)
    {
        bool released;
        lock (_lock)
        {
            released = _pendingCommits.Remove(context);
        }

        if (released)
        {
            _committed.TrySetResult();
        }
    }
}
