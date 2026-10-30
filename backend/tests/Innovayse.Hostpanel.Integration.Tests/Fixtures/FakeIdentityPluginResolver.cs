using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IIdentityPluginResolver"/> that hands back the plugin a test gave it, or
/// <see langword="null"/> for "no identity plugin enabled": a fake port standing in for the plugin host.
/// </summary>
/// <param name="plugin">The enabled plugin, or <see langword="null"/> for none.</param>
public sealed class FakeIdentityPluginResolver(IClientResolver? plugin) : IIdentityPluginResolver
{
    /// <inheritdoc/>
    public Task<IClientResolver?> ResolveAsync(CancellationToken ct) => Task.FromResult(plugin);
}
