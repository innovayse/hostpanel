using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>Outcome of an agent health check.</summary>
/// <param name="IsHealthy">Whether the agent answered and was installed for this server record.</param>
/// <param name="AgentVersion">The agent's version when it answered.</param>
/// <param name="Error">
/// Why the check failed, when it did; never host detail. <see cref="SshProviderFailure.BindingMismatch"/>
/// means the agent answering was installed for another server record.
/// </param>
/// <param name="Provider">
/// The profile id the agent reports it runs (a plugin's profile id or <c>generic-linux</c>), when it
/// answered. The agent's own name for its profile, passed through as it was reported.
/// </param>
public sealed record TestServerSshAgentResultDto(bool IsHealthy, string? AgentVersion, SshProviderFailure? Error, string? Provider);
