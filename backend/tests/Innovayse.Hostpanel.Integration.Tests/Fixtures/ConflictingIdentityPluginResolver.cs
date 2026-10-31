using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IIdentityPluginResolver"/> standing in for a deployment where two identity plugins are
/// enabled: it throws <see cref="IdentityPluginConflictException"/> exactly as the real resolver does.
/// </summary>
public sealed class ConflictingIdentityPluginResolver : IIdentityPluginResolver
{
    /// <inheritdoc/>
    public Task<IClientResolver?> ResolveAsync(CancellationToken ct) =>
        throw new IdentityPluginConflictException(["crm", "erp"]);
}
