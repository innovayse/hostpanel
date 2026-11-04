using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Billing;

/// <summary>
/// A client's account at one payment gateway, the reference the gateway's stored credential capability
/// saves and charges cards under. Stored in the <c>GatewayCustomers</c> table, one row per client and gateway
/// module.
/// </summary>
/// <remarks>
/// A client can use more than one gateway, so the reference lives beside the client, not on it. A unique index
/// on the client and the module, and another on the module and the reference, keep one gateway account from
/// ever belonging to two clients or to one client twice.
/// </remarks>
public sealed class GatewayCustomer : BaseEntity
{
    /// <summary>Gets the client the gateway account belongs to.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the payment gateway module the account is with (for example "stripe").</summary>
    public string GatewayModuleId { get; private set; } = string.Empty;

    /// <summary>Gets the gateway's own reference for the account.</summary>
    public string CustomerRef { get; private set; } = string.Empty;

    /// <summary>Gets when the account was linked to the client.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private GatewayCustomer() { }

    /// <summary>Links a client to its account at a payment gateway.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="gatewayModuleId">The payment gateway module the account is with.</param>
    /// <param name="customerRef">The gateway's reference for the account.</param>
    /// <param name="now">The current moment.</param>
    /// <returns>A new link.</returns>
    public static GatewayCustomer Create(int clientId, string gatewayModuleId, string customerRef, DateTimeOffset now) =>
        new()
        {
            ClientId = clientId,
            GatewayModuleId = gatewayModuleId,
            CustomerRef = customerRef,
            CreatedAt = now,
        };
}
