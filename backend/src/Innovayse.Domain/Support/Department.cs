namespace Innovayse.Domain.Support;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a support department that tickets can be routed to (e.g. "Technical Support", "Billing").
/// Created via <see cref="Create"/> factory — no public constructor.
/// </summary>
public sealed class Department : Entity
{
    /// <summary>Gets the display name of this department.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the email address associated with this department.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Department() : base(0) { }

    /// <summary>Internal constructor — use <see cref="Create"/> factory instead.</summary>
    /// <param name="name">The department display name.</param>
    /// <param name="email">The department email address.</param>
    private Department(string name, string email) : base(0)
    {
        Name = name;
        Email = email;
    }

    /// <summary>
    /// Updates the name and email of this department.
    /// </summary>
    /// <param name="name">The new department display name.</param>
    /// <param name="email">The new department email address.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="email"/> is null or whitespace.</exception>
    public void Update(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Name = name;
        Email = email;
    }

    /// <summary>
    /// Creates a new <see cref="Department"/> with the provided name and email.
    /// </summary>
    /// <param name="name">The department display name.</param>
    /// <param name="email">The department email address.</param>
    /// <returns>A new <see cref="Department"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="email"/> is null or whitespace.</exception>
    public static Department Create(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return new Department(name, email);
    }
}
