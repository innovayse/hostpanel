using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.SshAgent.Extensions;

/// <summary>Extension methods that turn a server's stored SSH settings into the endpoint its agent is reached at.</summary>
internal static class ServerSshTargetDtoExtensions
{
    /// <summary>Builds the agent endpoint of a server from its settings, for every adapter that talks to that agent.</summary>
    /// <param name="target">The server and its SSH settings.</param>
    /// <returns>The endpoint, with the timeouts the administrator configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the settings are missing or incomplete.</exception>
    public static SshAgentEndpoint ToAgentEndpoint(this ServerSshTargetDto target)
    {
        if (target.Agent is not { IsComplete: true } settings)
        {
            throw new InvalidOperationException($"SSH settings of server {target.ServerId} are missing or incomplete.");
        }

        return new SshAgentEndpoint(
            target.ServerId,
            settings.AgentHost,
            settings.AgentPort,
            settings.AgentUsername,
            settings.AgentPrivateKey!,
            settings.AgentHostKeyFingerprint,
            TimeSpan.FromSeconds(settings.ConnectTimeoutSeconds),
            TimeSpan.FromSeconds(settings.OperationTimeoutSeconds));
    }
}
