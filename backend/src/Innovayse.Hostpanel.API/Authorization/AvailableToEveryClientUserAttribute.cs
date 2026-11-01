namespace Innovayse.Hostpanel.API.Authorization;

/// <summary>
/// Marks a client portal action as open to every user of the account, whatever permissions they hold.
/// The explicit alternative to <see cref="RequiresClientPermissionAttribute"/>.
/// </summary>
/// <remarks>
/// Carries no behaviour: the action is still behind <c>Policies.Client</c>, so the caller must be
/// signed in with the client role. Its purpose is to make "needs no permission" a decision somebody
/// wrote down, which <c>ClientPortalPermissionTests</c> then checks for every client portal action.
/// Use it only for what the portal cannot open without, such as reading the profile it starts from, and for the one door
/// whose permission is not known until the request is matched: the client area plugin routes, where the use case enforces
/// the permission the matched route declares (<c>CallClientPluginHandler</c>).
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AvailableToEveryClientUserAttribute : Attribute
{
}
