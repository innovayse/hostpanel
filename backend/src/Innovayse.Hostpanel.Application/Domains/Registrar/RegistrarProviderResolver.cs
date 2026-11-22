using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Registrar;

/// <summary>
/// Application service that chooses the registrar for a domain or a domain name: the one place that decides which
/// registrar plugin answers. It looks the TLD up in the TLD configuration table with longest-suffix matching, and
/// refuses through an unavailable provider when no configuration exists, never by guessing a registrar.
/// </summary>
public sealed class RegistrarProviderResolver
{
    /// <summary>Database context used to read the TLD configuration.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Builds the provider the chosen module names.</summary>
    private readonly IRegistrarProviderFactory _factory;

    /// <summary>Tells which optional operations the chosen module offers.</summary>
    private readonly IRegistrarModuleCatalogue _modules;

    /// <summary>Initializes a new instance of the <see cref="RegistrarProviderResolver"/> class.</summary>
    /// <param name="context">Database context used to read the TLD configuration.</param>
    /// <param name="factory">Builds the provider the chosen module names.</param>
    /// <param name="modules">Tells which optional operations the chosen module offers.</param>
    public RegistrarProviderResolver(
        IHostpanelDbContext context,
        IRegistrarProviderFactory factory,
        IRegistrarModuleCatalogue modules)
    {
        _context = context;
        _factory = factory;
        _modules = modules;
    }

    /// <summary>
    /// Resolves the registrar provider for a domain that already exists. The registrar the domain was
    /// placed at (<see cref="RegisteredDomain.RegistrarModule"/>) wins, so a domain keeps going to its
    /// registrar after its TLD is configured for another one; a domain without a stored module (a
    /// row that predates the column, or an import whose registrar is unknown) follows its TLD.
    /// </summary>
    /// <param name="domain">The stored domain to resolve a provider for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="IRegistrarProvider"/> that holds the domain.</returns>
    public async Task<IRegistrarProvider> ResolveAsync(RegisteredDomain domain, CancellationToken ct)
    {
        return await _factory.GetProviderAsync(await ResolveModuleAsync(domain, ct), ct);
    }

    /// <summary>
    /// Tells which optional operations the registrar that holds a domain offers, read from the plugin's manifest alone,
    /// so no plugin code runs and a list may ask for every row.
    /// </summary>
    /// <param name="domain">The stored domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The offered operations; none when no installed registrar plugin holds the domain.</returns>
    public async Task<RegistrarCapabilitiesDto> ResolveCapabilitiesAsync(RegisteredDomain domain, CancellationToken ct)
    {
        return _modules.CapabilitiesOf(await ResolveModuleAsync(domain, ct));
    }

    /// <summary>
    /// Resolves the registrar for a domain name by looking up its TLD in the TldConfig table.
    /// Uses longest-suffix matching (e.g. "co.am" before "am").
    /// A name whose TLD has no configuration resolves to a provider that refuses every operation. Used for a name
    /// that has no stored domain yet (availability, WHOIS, a new registration or transfer) and by
    /// <see cref="ResolveAsync(RegisteredDomain, CancellationToken)"/> for a domain with no stored module.
    /// </summary>
    /// <param name="domainName">The fully qualified domain name to resolve a registrar for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider responsible for the name's TLD and the module that names it, or no module when none is configured.</returns>
    public async Task<ResolvedRegistrar> ResolveForTldAsync(string domainName, CancellationToken ct)
    {
        var module = await ResolveModuleForTldAsync(domainName, ct);

        return new ResolvedRegistrar(await _factory.GetProviderAsync(module, ct), module);
    }

    /// <summary>The module a domain is managed through: the one stored on it, else the one its TLD is configured for.</summary>
    /// <param name="domain">The stored domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The module, or <see langword="null"/> when neither names one.</returns>
    private async Task<string?> ResolveModuleAsync(RegisteredDomain domain, CancellationToken ct)
    {
        return domain.RegistrarModule ?? await ResolveModuleForTldAsync(domain.Name, ct);
    }

    /// <summary>
    /// The module the longest configured suffix of a name is sourced from, without building the registrar: for a caller that
    /// only needs to record where a domain belongs.
    /// </summary>
    /// <param name="domainName">The fully qualified domain name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The module, or <see langword="null"/> when no suffix of the name is configured.</returns>
    public async Task<string?> ResolveModuleForTldAsync(string domainName, CancellationToken ct)
    {
        foreach (var tld in ExtractTldCandidates(domainName))
        {
            var normalizedTld = tld.ToLower().Trim();
            var config = await _context.TldConfigs.FirstOrDefaultAsync(x => x.Tld == normalizedTld, ct);
            if (config is not null)
            {
                return config.RegistrarModule;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts all possible TLD suffixes from a domain name, ordered longest first.
    /// For "example.co.am" returns ["co.am", "am"].
    /// For "test.example.com" returns ["example.com", "com"].
    /// </summary>
    /// <param name="domainName">The fully qualified domain name to extract TLD candidates from.</param>
    /// <returns>A list of TLD suffixes ordered by length descending.</returns>
    internal static List<string> ExtractTldCandidates(string domainName)
    {
        var parts = domainName.ToLowerInvariant().Trim().Split('.');
        var candidates = new List<string>();

        // Start from index 1 (skip the SLD), generate suffixes longest first
        for (var i = 1; i < parts.Length; i++)
        {
            candidates.Add(string.Join('.', parts[i..]));
        }

        return candidates;
    }
}
