namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>A Plesk customer account, as far as provisioning needs to know it.</summary>
/// <param name="Login">The customer's login, which is the service's provisioning reference.</param>
/// <param name="IsSuspended">Whether the customer is disabled, which also disables its subscriptions.</param>
public sealed record PleskCustomer(string Login, bool IsSuspended);
