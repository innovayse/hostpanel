using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;

namespace Innovayse.Hostpanel.Application.Domains.Registrar;

/// <summary>
/// The one place that answers which extra fields a registrar plugin asks for a domain: it finds the registrar the way every
/// other domain operation does (the module stored on the domain, else the one its extension is configured for), reads that
/// plugin's manifest and lets the SDK's own rule choose the set of fields. Nothing here names a zone or a country, whatever
/// the registrar asks comes from the manifest.
/// </summary>
public sealed class DomainFieldReader
{
    /// <summary>Resolves the registrar module a domain name is configured for.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Reads the manifest of the installed registrar plugin.</summary>
    private readonly IRegistrarDomainFieldCatalogue _catalogue;

    /// <summary>Initializes a new instance of the <see cref="DomainFieldReader"/> class.</summary>
    /// <param name="registrars">Resolves the registrar module a domain name is configured for.</param>
    /// <param name="catalogue">Reads the manifest of the installed registrar plugin.</param>
    public DomainFieldReader(RegistrarProviderResolver registrars, IRegistrarDomainFieldCatalogue catalogue)
    {
        _registrars = registrars;
        _catalogue = catalogue;
    }

    /// <summary>Answers the fields the registrar asks for one domain, as the manifest declares them.</summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="module">The registrar module the domain is held at, or <see langword="null"/> to follow the name's extension.</param>
    /// <param name="holder">The kind of registrant.</param>
    /// <param name="operation">The one operation about to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fields in declared order; empty when no registrar plugin is installed for the name or the manifest asks for none.</returns>
    public async Task<IReadOnlyList<PluginDomainField>> MatchAsync(
        string domainName, string? module, PluginDomainHolder holder, PluginDomainOperations operation, CancellationToken ct)
    {
        var manifest = await ManifestAsync(domainName, module, ct);
        return manifest is null ? [] : PluginDomainFieldRules.Match(manifest, domainName, holder, operation);
    }

    /// <summary>Answers the fields the registrar asks for one domain, with their texts in the caller's language.</summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="module">The registrar module the domain is held at, or <see langword="null"/> to follow the name's extension.</param>
    /// <param name="holder">The kind of registrant.</param>
    /// <param name="operation">The one operation about to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fields in declared order; empty when no registrar plugin is installed for the name or the manifest asks for none.</returns>
    public async Task<IReadOnlyList<DomainFieldDto>> DescribeAsync(
        string domainName, string? module, PluginDomainHolder holder, PluginDomainOperations operation, CancellationToken ct)
    {
        var manifest = await ManifestAsync(domainName, module, ct);
        if (manifest is null)
        {
            return [];
        }

        return PluginDomainFieldRules.Match(manifest, domainName, holder, operation)
            .Select(field => field.ToDto(manifest))
            .ToList();
    }

    /// <summary>Finds the manifest of the registrar plugin that holds a domain.</summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="module">The registrar module the domain is held at, or <see langword="null"/> to follow the name's extension.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The manifest, or <see langword="null"/> when no installed registrar plugin holds the name.</returns>
    private async Task<PluginManifest?> ManifestAsync(string domainName, string? module, CancellationToken ct)
    {
        return _catalogue.ManifestOf(module ?? await _registrars.ResolveModuleForTldAsync(domainName, ct));
    }
}
