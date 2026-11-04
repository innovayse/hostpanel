namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>A key the host issued to a plugin on a fresh bound service, for the plugin SSH revoke tests.</summary>
/// <param name="Owner">The owner of the service the key is on.</param>
/// <param name="KeyId">The issued key's id.</param>
public sealed record IssuedPluginSshKey(SshOwnerScenario Owner, Guid KeyId);
