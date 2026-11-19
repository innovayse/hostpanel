using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Resolves the website and database capability of a server. A server has it when its module has an agent profile,
/// the same rule as <see cref="ISshKeyProviderFactory.Supports"/>: the agent that holds the keys sets up the sites.
/// </summary>
public interface IHostingResourceProviderFactory
{
    /// <summary>Creates a provider bound to one server.</summary>
    /// <param name="target">The server and its agent settings; the settings must be present and complete.</param>
    /// <returns>The provider, or <see langword="null"/> when the server's module is unsupported.</returns>
    public IHostingResourceProvider? CreateFor(ServerSshTargetDto target);
}
