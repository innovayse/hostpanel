using Innovayse.SDK.Plugins.Fulfilment;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IFulfilmentPlugin"/> a test drives entirely: it records every context it is handed, in
/// the order the calls came, and answers, throws or waits as the test set it up. A stub standing in for a
/// third-party fulfilment plugin, which a test must never load from disk.
/// </summary>
public sealed class StubFulfilmentPlugin : IFulfilmentPlugin
{
    /// <summary>The calls received, in order: the method name and the invoice id it carried.</summary>
    private readonly List<(string Method, int InvoiceId)> _calls = [];

    /// <summary>Gets the calls received so far, in order.</summary>
    public IReadOnlyList<(string Method, int InvoiceId)> Calls => _calls;

    /// <summary>Gets the context of the latest paid call, or <see langword="null"/> when there was none.</summary>
    public InvoicePaidContext? LastPaid { get; private set; }

    /// <summary>Gets the context of the latest refunded call, or <see langword="null"/> when there was none.</summary>
    public InvoiceRefundedContext? LastRefunded { get; private set; }

    /// <summary>Gets the context of the latest lapsed call, or <see langword="null"/> when there was none.</summary>
    public SubscriptionLapsedContext? LastLapsed { get; private set; }

    /// <summary>
    /// Gets or sets what every call does after it was recorded: throw, wait for the token, or return.
    /// Unset, a call returns at once.
    /// </summary>
    public Func<string, int, CancellationToken, Task>? Behaviour { get; set; }

    /// <inheritdoc/>
    public Task OnInvoicePaidAsync(InvoicePaidContext context, CancellationToken ct)
    {
        LastPaid = context;
        return RunAsync(nameof(OnInvoicePaidAsync), context.InvoiceId, ct);
    }

    /// <inheritdoc/>
    public Task<RefundAdvice> AdviseRefundAsync(RefundAdviceContext context, CancellationToken ct)
    {
        return Task.FromResult(new RefundAdvice(context.RequestedAmount, null));
    }

    /// <inheritdoc/>
    public Task OnInvoiceRefundedAsync(InvoiceRefundedContext context, CancellationToken ct)
    {
        LastRefunded = context;
        return RunAsync(nameof(OnInvoiceRefundedAsync), context.InvoiceId, ct);
    }

    /// <inheritdoc/>
    public Task OnSubscriptionLapsedAsync(SubscriptionLapsedContext context, CancellationToken ct)
    {
        LastLapsed = context;
        return RunAsync(nameof(OnSubscriptionLapsedAsync), context.InvoiceId, ct);
    }

    /// <summary>Records a call, then does what <see cref="Behaviour"/> says.</summary>
    /// <param name="method">The plugin method called.</param>
    /// <param name="invoiceId">The invoice the call carried.</param>
    /// <param name="ct">The call cancellation token.</param>
    /// <returns>A task that completes when the behaviour does.</returns>
    private Task RunAsync(string method, int invoiceId, CancellationToken ct)
    {
        _calls.Add((method, invoiceId));
        return Behaviour?.Invoke(method, invoiceId, ct) ?? Task.CompletedTask;
    }
}
