using System.Security.Claims;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="ICurrentUserService"/> for code that runs outside a request (a background job): a signed-in
/// subject with no <c>HttpContext</c> behind it.
/// </summary>
/// <param name="subject">The subject the caller is signed in as.</param>
public sealed class FixedUser(string subject) : ICurrentUserService
{
    /// <inheritdoc/>
    public ClaimsPrincipal? Principal { get; } = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], "Test"));

    /// <inheritdoc/>
    public string? UserId => subject;

    /// <inheritdoc/>
    public string? Email => null;

    /// <inheritdoc/>
    public bool? IsEmailVerified => null;

    /// <inheritdoc/>
    public string? IpAddress => null;

    /// <inheritdoc/>
    public bool IsAuthenticated => true;

    /// <inheritdoc/>
    public Task<string?> GetUserNameAsync(CancellationToken ct) => Task.FromResult<string?>(null);

    /// <inheritdoc/>
    public string RequireUserId() => subject;
}
