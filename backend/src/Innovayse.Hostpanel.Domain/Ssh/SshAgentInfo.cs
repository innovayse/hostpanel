namespace Innovayse.Hostpanel.Domain.Ssh;

/// <summary>What an agent reports about itself on ping.</summary>
/// <param name="Version">Agent version.</param>
/// <param name="Provider">The provider profile it runs (<c>cwp</c>, <c>generic-linux</c> and so on).</param>
/// <param name="Binding">The server record it was installed for, or null when the install predates bindings.</param>
public sealed record SshAgentInfo(string Version, string? Provider, Guid? Binding);
