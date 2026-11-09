namespace Innovayse.Hostpanel.Shared.Authorization;

/// <summary>
/// The authorization policy names every controller action names in <c>[Authorize(Policy = name)]</c>.
/// Registered once, in <c>Program.cs</c>, over hostpanel's roles.
/// </summary>
/// <remarks>
/// The roles are the staff <c>Admin</c>, the <c>Reseller</c> and the <c>Client</c>. The <c>Reseller</c> role has no policy of its own: a reseller is linked to
/// no clients (no <c>ResellerId</c> on any record), so there is no data to scope a reseller to and every staff
/// route is <see cref="Admin"/> only, except the read-only routes under <see cref="Staff"/> that name no client data.
/// </remarks>
public static class Policies
{
    /// <summary>Any signed-in caller, whatever their role.</summary>
    public const string Member = "Member";

    /// <summary>Staff: the admin panel's operators.</summary>
    public const string Admin = "Admin";

    /// <summary>Any signed-in staff role, <c>Admin</c> or <c>Reseller</c>, for read-only routes every operator may use; never a client.</summary>
    public const string Staff = "Staff";

    /// <summary>A client of the portal acting on its own account.</summary>
    public const string Client = "Client";

    /// <summary>A service-to-service caller on an <c>/internal/*</c> route, authenticated by service key.</summary>
    public const string InternalService = "InternalService";
}
