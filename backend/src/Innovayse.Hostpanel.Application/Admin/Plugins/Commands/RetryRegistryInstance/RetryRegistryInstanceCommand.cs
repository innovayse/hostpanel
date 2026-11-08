using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RetryRegistryInstance;

/// <summary>
/// Runs the registration with the plugin registry again at once, ignoring the back-off, and registers anew after a revocation.
/// The button of the registration card on the plugins page.
/// </summary>
public sealed record RetryRegistryInstanceCommand : IDemoRestricted;
