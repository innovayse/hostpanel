namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>What the registry answers to a registration: the id of the new instance and the challenge it wants served.</summary>
/// <param name="Id">The instance id.</param>
/// <param name="Challenge">The text this installation has to serve at its well-known address.</param>
public sealed record RegistryInstanceRegistration(Guid Id, string Challenge);
