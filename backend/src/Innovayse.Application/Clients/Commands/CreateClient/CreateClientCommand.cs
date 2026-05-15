namespace Innovayse.Application.Clients.Commands.CreateClient;

/// <summary>
/// Command to create a new client record.
/// Used by admin to create a client manually, and by <c>CreateClientOnRegisterHandler</c>
/// to auto-create a client on user registration.
/// </summary>
/// <param name="UserId">The Identity user ID to link to this client.</param>
/// <param name="Email">The user's email address (stored on domain event).</param>
/// <param name="FirstName">Client's first name.</param>
/// <param name="LastName">Client's last name.</param>
/// <param name="CompanyName">Optional company name.</param>
public record CreateClientCommand(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? CompanyName = null);
