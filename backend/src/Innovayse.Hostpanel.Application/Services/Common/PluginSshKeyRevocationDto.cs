namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>What revoking a removed plugin's SSH keys did.</summary>
/// <param name="Revoked">Keys the agents confirmed gone.</param>
/// <param name="Failed">Keys still live: the agent did not confirm, and the key records why.</param>
public sealed record PluginSshKeyRevocationDto(int Revoked, int Failed);
