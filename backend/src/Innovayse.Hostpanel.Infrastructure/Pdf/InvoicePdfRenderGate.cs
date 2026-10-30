using Innovayse.Hostpanel.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Pdf;

/// <summary>
/// The process-wide limit on how many invoice PDFs render at once.
/// </summary>
/// <remarks>
/// <para>
/// Rendering is synchronous CPU work that holds a thread-pool thread from start to finish. The
/// per-caller rate-limit policy stops one client from starting many, but many clients at once, or
/// staff on the admin route, which has no policy, could still occupy every pool thread. One gate
/// for the whole process caps that at <see cref="InvoicePdfOptions.MaxConcurrentRenders"/>.
/// </para>
/// <para>
/// A render over the cap waits asynchronously, holding no thread, and leaves the queue if its
/// request is cancelled. The wait is bounded by <see cref="InvoicePdfOptions.MaxRenderWaitSeconds"/>:
/// a render still queued when it runs out is turned away, so a burst cannot keep requests open for
/// as long as it lasts. Registered as a singleton, so every renderer shares the one semaphore; the
/// container disposes it at shutdown.
/// </para>
/// </remarks>
public sealed class InvoicePdfRenderGate : IDisposable
{
    /// <summary>One permit per render allowed in flight.</summary>
    private readonly SemaphoreSlim _slots;

    /// <summary>Initializes the gate.</summary>
    /// <param name="options">How many renders may run at once, and how long one waits for a slot.</param>
    public InvoicePdfRenderGate(IOptions<InvoicePdfOptions> options)
    {
        Limit = options.Value.MaxConcurrentRenders;
        MaxWait = TimeSpan.FromSeconds(options.Value.MaxRenderWaitSeconds);
        _slots = new SemaphoreSlim(Limit, Limit);
    }

    /// <summary>How many renders may run at once.</summary>
    public int Limit { get; }

    /// <summary>How long a render waits for a free slot before it is turned away.</summary>
    public TimeSpan MaxWait { get; }

    /// <summary>Waits up to <see cref="MaxWait"/> for a free slot and takes it.</summary>
    /// <param name="ct">Cancels the wait; a cancelled wait never takes a slot.</param>
    /// <returns>The slot, given back when it is disposed; null when none freed within <see cref="MaxWait"/>.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="ct"/> is cancelled first.</exception>
    public async Task<IDisposable?> TryEnterAsync(CancellationToken ct)
    {
        return await _slots.WaitAsync(MaxWait, ct) ? new InvoicePdfRenderSlot(_slots) : null;
    }

    /// <inheritdoc/>
    public void Dispose() => _slots.Dispose();
}
