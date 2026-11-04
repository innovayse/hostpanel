using System.Text;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// Where and how to reach a server's SSH key agent, as an SSH key provider needs it. Not a DTO:
/// it carries the agent private key, so it stays server-side and is never sent to a browser.
/// </summary>
/// <remarks>
/// Public rather than internal because the Infrastructure adapters (the key provider factory and
/// the service context resolver) read it through <see cref="ServerSshTargetDto"/>.
/// </remarks>
/// <param name="IsEnabled">Whether customers may manage keys.</param>
/// <param name="IsComplete">Whether every value an agent call needs is present.</param>
/// <param name="SshHostname">Customer-facing hostname.</param>
/// <param name="SshPort">Customer-facing port.</param>
/// <param name="AgentHost">Agent host.</param>
/// <param name="AgentPort">Agent SSH port.</param>
/// <param name="AgentUsername">Management user.</param>
/// <param name="AgentPrivateKey">Management private key, when stored.</param>
/// <param name="AgentHostKeyFingerprint">Pinned agent host key fingerprint.</param>
/// <param name="ConnectTimeoutSeconds">Connect timeout in seconds.</param>
/// <param name="OperationTimeoutSeconds">Operation timeout in seconds.</param>
public sealed record ServerSshAgentEndpoint(
    bool IsEnabled,
    bool IsComplete,
    string SshHostname,
    int SshPort,
    string AgentHost,
    int AgentPort,
    string AgentUsername,
    string? AgentPrivateKey,
    string AgentHostKeyFingerprint,
    int ConnectTimeoutSeconds,
    int OperationTimeoutSeconds)
{
    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
