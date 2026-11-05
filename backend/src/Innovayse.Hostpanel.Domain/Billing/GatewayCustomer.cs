using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// The customer a payment gateway module keeps for one client: the reference the plugin returned the first time the
/// host asked for the client's customer. One client has at most one customer per gateway module. Stored in the
/// <c>GatewayCustomers</c> table, so a second gateway needs no new column on the client.
/// </summary>
public sealed class GatewayCustomer : BaseEntity, IAggregateRoot
{
    /// <summary>The longest module id or customer reference the columns hold, in characters.</summary>
    public const int MaxLength = 200;

    /// <summary>Gets the client the customer belongs to.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the id of the payment plugin whose gateway holds the customer.</summary>
    public string GatewayModuleId { get; private set; } = string.Empty;

    /// <summary>Gets the gateway's reference for the customer, opaque to the host.</summary>
    public string CustomerRef { get; private set; } = string.Empty;

    /// <summary>Gets when the customer was recorded (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private GatewayCustomer() { }

    /// <summary>Records the customer a gateway module returned for a client.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="gatewayModuleId">The payment plugin id.</param>
    /// <param name="customerRef">The gateway's customer reference.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <returns>A new, unsaved <see cref="GatewayCustomer"/>.</returns>
    /// <exception cref="ArgumentException">A text argument is empty, white space or longer than <see cref="MaxLength"/>.</exception>
    public static GatewayCustomer Create(int clientId, string gatewayModuleId, string customerRef, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayModuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerRef);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(gatewayModuleId.Length, MaxLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(customerRef.Length, MaxLength);

        return new GatewayCustomer
        {
            ClientId = clientId,
            GatewayModuleId = gatewayModuleId,
            CustomerRef = customerRef,
            CreatedAt = now,
        };
    }
}
