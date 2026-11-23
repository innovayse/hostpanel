using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Services;

/// <summary>
/// The record that a client has used the free trial of a product. A client gets one trial of a product, ever, so the
/// row stays after the service it started is gone. Stored in the <c>ClientProductTrials</c> table.
/// </summary>
public sealed class ClientProductTrial : BaseEntity
{
    /// <summary>Gets the client the trial was given to.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the product the trial was of.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets when the trial started (UTC).</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private ClientProductTrial()
    {
    }

    /// <summary>Records that a client starts the trial of a product.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="productId">The product.</param>
    /// <param name="now">The moment the trial starts.</param>
    /// <returns>The new record.</returns>
    public static ClientProductTrial Create(int clientId, int productId, DateTimeOffset now) =>
        new() { ClientId = clientId, ProductId = productId, StartedAt = now };
}
