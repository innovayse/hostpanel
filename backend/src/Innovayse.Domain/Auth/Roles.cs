namespace Innovayse.Domain.Auth;

/// <summary>
/// Static role name constants used across the application.
/// These strings must match the seeded <c>IdentityRole</c> names.
/// </summary>
public static class Roles
{
    /// <summary>Full system administrator — unrestricted access.</summary>
    public const string Admin = "Admin";

    /// <summary>Reseller — can manage their own clients and services.</summary>
    public const string Reseller = "Reseller";

    /// <summary>End client — access to their own portal only.</summary>
    public const string Client = "Client";
}
