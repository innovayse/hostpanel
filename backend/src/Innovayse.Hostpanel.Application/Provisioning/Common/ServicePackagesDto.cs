namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>The hosting packages the server of a provisioned service offers.</summary>
/// <param name="Names">The package names exactly as the panel knows them, in the panel's order; empty when it has none.</param>
public sealed record ServicePackagesDto(IReadOnlyList<string> Names);
