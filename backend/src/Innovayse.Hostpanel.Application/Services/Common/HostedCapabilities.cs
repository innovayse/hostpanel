namespace Innovayse.Hostpanel.Application.Services.Common;

/// <summary>What the hosting account of one service can do for the plugin that asked, as the host answers it.</summary>
/// <param name="Ssh">Whether SSH access can be issued for the service.</param>
/// <param name="Websites">Whether websites can be created on the service.</param>
/// <param name="Databases">Whether databases can be created on the service.</param>
/// <param name="PhpVersions">The PHP versions a website of the service can use; empty when none.</param>
public sealed record HostedCapabilities(bool Ssh, bool Websites, bool Databases, IReadOnlyList<string> PhpVersions);
