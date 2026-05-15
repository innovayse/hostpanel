namespace Innovayse.Domain.Clients;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a pending invitation for a new user to join a client account.
/// Created by an admin or account owner, consumed when the invitee registers.
/// </summary>
public sealed class Invitation : Entity
{
    /// <summary>Gets the FK to the client account the user is being invited to.</summary>
    public int ClientId { get; private set; }

    /// <summary>Gets the unique, unguessable token used to accept the invitation.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Gets the email address of the invited user.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Gets the first name of the invited user.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Gets the last name of the invited user.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>Gets the permissions that will be granted upon acceptance.</summary>
    public ClientPermission Permissions { get; private set; }

    /// <summary>Gets the UTC timestamp when this invitation expires.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the UTC timestamp when this invitation was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when this invitation was accepted, or <see langword="null"/> if still pending.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Gets a value indicating whether this invitation has expired.</summary>
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    /// <summary>Gets a value indicating whether this invitation has been accepted.</summary>
    public bool IsAccepted => AcceptedAt.HasValue;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Invitation() : base(0) { }

    /// <summary>
    /// Creates a new invitation for a user to join a client account.
    /// </summary>
    /// <param name="clientId">FK to the client account.</param>
    /// <param name="email">Email address of the invitee.</param>
    /// <param name="firstName">First name of the invitee.</param>
    /// <param name="lastName">Last name of the invitee.</param>
    /// <param name="permissions">Permissions to grant upon acceptance.</param>
    /// <returns>A new <see cref="Invitation"/> instance with a generated token and 7-day expiry.</returns>
    public static Invitation Create(
        int clientId,
        string email,
        string firstName,
        string lastName,
        ClientPermission permissions)
    {
        return new Invitation
        {
            ClientId = clientId,
            Token = Guid.NewGuid().ToString("N"),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Permissions = permissions,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };
    }

    /// <summary>
    /// Marks this invitation as accepted, setting <see cref="AcceptedAt"/> to the current UTC time.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the invitation has already expired or has already been accepted.
    /// </exception>
    public void Accept()
    {
        if (IsExpired)
        {
            throw new InvalidOperationException("This invitation has expired.");
        }

        if (IsAccepted)
        {
            throw new InvalidOperationException("This invitation has already been accepted.");
        }

        AcceptedAt = DateTimeOffset.UtcNow;
    }
}
