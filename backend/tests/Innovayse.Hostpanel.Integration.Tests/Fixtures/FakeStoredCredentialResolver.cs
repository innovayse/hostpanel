using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.SDK.Plugins.StoredCredentials;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Serves <see cref="FakeStoredCredentialPlugin"/> as the capability of one gateway module and nothing for any other,
/// the way a real resolver answers for a module that is not installed or does not offer saved methods. The module is
/// not the built-in one, so the integrations switch of the built-in gateway never gets in the way of a test.
/// </summary>
/// <param name="plugin">The scripted capability.</param>
public sealed class FakeStoredCredentialResolver(FakeStoredCredentialPlugin plugin) : IStoredCredentialPluginResolver
{
    /// <summary>The module id the fake answers for.</summary>
    public const string Module = "fake-gateway";

    /// <summary>Gets or sets whether the module currently offers the capability (a plugin can be disabled at any time).</summary>
    public bool Offered { get; set; } = true;

    /// <inheritdoc/>
    public Task<IStoredCredentialPlugin?> ResolveAsync(string module, CancellationToken ct) =>
        Task.FromResult<IStoredCredentialPlugin?>(Offered && module == Module ? plugin : null);
}
