namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>A Plesk subscription (webspace) owned by a customer.</summary>
/// <param name="Id">Plesk's id of the subscription.</param>
/// <param name="Domain">The subscription's primary domain.</param>
public sealed record PleskSubscription(int Id, string Domain);
