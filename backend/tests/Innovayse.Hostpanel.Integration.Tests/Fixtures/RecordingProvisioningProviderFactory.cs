using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IProvisioningProviderFactory"/> that hands back the same
/// <see cref="RecordingProvisioningProvider"/> for every server, so a test can read the calls a
/// domain event handler made through it regardless of which server row triggered them.
/// </summary>
/// <param name="provider">The recording provider every server resolves to.</param>
public sealed class RecordingProvisioningProviderFactory(RecordingProvisioningProvider provider)
    : IProvisioningProviderFactory
{
    /// <summary>Every module the tests seed servers on: the double stands in for whichever panel that is.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The host's modules and the second test panel.</returns>
    public Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct) =>
        Task.FromResult(TestPanelModules.Supported);

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => provider;
}
