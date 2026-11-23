namespace Innovayse.Hostpanel.Application.Domains.Common;

/// <summary>
/// Switches a feature (email forwarding or DNS management) of a domain at the registrar.
/// </summary>
/// <param name="Domain">The domain at the registrar.</param>
/// <param name="Enabled">Whether the feature should be on.</param>
public sealed record DomainFeatureToggle(DomainRef Domain, bool Enabled);
