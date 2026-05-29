namespace Innovayse.Application.Common;

/// <summary>
/// Provides information about the currently authenticated admin performing an HTTP request.
/// Implemented in Infrastructure via <c>IHttpContextAccessor</c>.
/// </summary>
public interface ICurrentRequestContext
{
    /// <summary>Gets the Identity user ID of the current admin, or <see langword="null"/> if unauthenticated.</summary>
    string? AdminId { get; }

    /// <summary>Gets the display name of the current admin, or <see langword="null"/> if unavailable.</summary>
    string? AdminName { get; }

    /// <summary>Gets the email of the current admin, or <see langword="null"/> if unavailable.</summary>
    string? AdminEmail { get; }

    /// <summary>Gets the remote IP address of the current request, or <see langword="null"/> if unavailable.</summary>
    string? IpAddress { get; }
}
