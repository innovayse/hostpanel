namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.GetRegistryChallenge;

/// <summary>
/// Reads the text the plugin registry fetches from <c>/.well-known/hostpanel-registry/{id}</c> to confirm this installation.
/// Anonymous by design: the registry has no credential for this installation yet, and the challenge only proves control of the address.
/// </summary>
/// <param name="InstanceId">The instance id; set from the route.</param>
public sealed record GetRegistryChallengeQuery(Guid InstanceId);
