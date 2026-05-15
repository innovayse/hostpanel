namespace Innovayse.Application.Domains.Events;

using Innovayse.Domain.Domains.Events;

/// <summary>
/// Handles <see cref="DomainRenewedEvent"/> raised when a domain is successfully renewed.
/// </summary>
public sealed class DomainRenewedHandler
{
    /// <summary>
    /// Handles the domain renewed event.
    /// </summary>
    /// <param name="evt">The domain renewed event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public Task HandleAsync(DomainRenewedEvent evt, CancellationToken ct)
    {
        // TODO: reactivate suspended linked service
        return Task.CompletedTask;
    }
}
