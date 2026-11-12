using Innovayse.Hostpanel.Domain.Billing;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The client's customer at a payment gateway module, as <see cref="GatewayCustomerEnsurer"/> found or created it. When
/// the customer is new its row is only staged on the context: the caller saves it together with the rest of its own
/// change, through <see cref="GatewayCustomerEnsurer.SaveAsync"/>.
/// </summary>
/// <param name="ClientId">The client the customer belongs to.</param>
/// <param name="GatewayModuleId">The payment plugin id whose gateway holds the customer.</param>
/// <param name="CustomerRef">The gateway's reference for the customer, opaque to the host.</param>
/// <param name="Created">The customer row staged by this call, or <see langword="null"/> when the host already held the customer.</param>
public sealed record EnsuredGatewayCustomer(int ClientId, string GatewayModuleId, string CustomerRef, GatewayCustomer? Created);
