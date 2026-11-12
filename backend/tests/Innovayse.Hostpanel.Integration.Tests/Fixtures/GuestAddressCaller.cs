using System.Security.Claims;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>An anonymous caller whose request came from a fixed address, as a guest checkout or a registration does.</summary>
/// <param name="ipAddress">The address the caller's request is resolved to.</param>
public sealed class GuestAddressCaller(string? ipAddress) : ICurrentUserService
{
    /// <inheritdoc/>
    public ClaimsPrincipal? Principal => null;

    /// <inheritdoc/>
    public string? UserId => null;

    /// <inheritdoc/>
    public string? Email => null;

    /// <inheritdoc/>
    public bool? IsEmailVerified => null;

    /// <inheritdoc/>
    public string? IpAddress => ipAddress;

    /// <inheritdoc/>
    public bool IsAuthenticated => false;

    /// <inheritdoc/>
    public Task<string?> GetUserNameAsync(CancellationToken ct) => Task.FromResult<string?>(null);

    /// <inheritdoc/>
    public string RequireUserId() => throw new UnauthorizedAccessException();
}
