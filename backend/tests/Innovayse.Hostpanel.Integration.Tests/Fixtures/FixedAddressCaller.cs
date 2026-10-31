using System.Security.Claims;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A signed-in staff caller whose request came from a fixed address.</summary>
/// <param name="ipAddress">The address the caller's request is resolved to.</param>
public sealed class FixedAddressCaller(string? ipAddress) : ICurrentUserService
{
    /// <inheritdoc/>
    public ClaimsPrincipal? Principal => null;

    /// <inheritdoc/>
    public string? UserId => "staff-1";

    /// <inheritdoc/>
    public string? Email => "staff@example.test";

    /// <inheritdoc/>
    public bool? IsEmailVerified => true;

    /// <inheritdoc/>
    public string? IpAddress => ipAddress;

    /// <inheritdoc/>
    public bool IsAuthenticated => true;

    /// <inheritdoc/>
    public Task<string?> GetUserNameAsync(CancellationToken ct) => Task.FromResult<string?>(Email);

    /// <inheritdoc/>
    public string RequireUserId() => "staff-1";
}
