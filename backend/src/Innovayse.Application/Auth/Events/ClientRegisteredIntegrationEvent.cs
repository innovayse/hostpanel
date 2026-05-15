namespace Innovayse.Application.Auth.Events;

/// <summary>
/// Integration event published by <c>RegisterHandler</c> after a new Identity user is created.
/// Wolverine delivers it to <c>CreateClientOnRegisterHandler</c> in the Clients module.
/// </summary>
/// <param name="UserId">The new user's Identity ID.</param>
/// <param name="Email">The new user's email address.</param>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
public record ClientRegisteredIntegrationEvent(string UserId, string Email, string FirstName, string LastName);
