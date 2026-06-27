namespace Innovayse.Application.Domains.Services;

using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Application service that resolves the correct <see cref="IRegistrarProvider"/>
/// for a domain name by looking up its TLD in the <see cref="ITldConfigRepository"/>.
/// Uses longest-suffix matching and falls back to the default provider when no configuration exists.
/// </summary>
public sealed class RegistrarProviderResolver(
    ITldConfigRepository tldConfigRepo,
    IRegistrarProviderFactory factory,
    IRegistrarProvider defaultProvider)
{
    /// <summary>
    /// Resolves the registrar provider for a domain by looking up its TLD in the TldConfig table.
    /// Uses longest-suffix matching (e.g. "co.am" before "am").
    /// Falls back to the default provider if no TldConfig exists.
    /// </summary>
    /// <param name="domainName">The fully qualified domain name to resolve a provider for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <see cref="IRegistrarProvider"/> responsible for the domain's TLD.</returns>
    public async Task<IRegistrarProvider> ResolveAsync(string domainName, CancellationToken ct)
    {
        var candidates = ExtractTldCandidates(domainName);

        foreach (var tld in candidates)
        {
            var config = await tldConfigRepo.FindByTldAsync(tld, ct);
            if (config is not null)
            {
                return factory.GetProvider(config.RegistrarModule);
            }
        }

        return defaultProvider;
    }

    /// <summary>
    /// Extracts all possible TLD suffixes from a domain name, ordered longest first.
    /// For "example.co.am" returns ["co.am", "am"].
    /// For "test.example.com" returns ["example.com", "com"].
    /// </summary>
    /// <param name="domainName">The fully qualified domain name to extract TLD candidates from.</param>
    /// <returns>A list of TLD suffixes ordered by length descending.</returns>
    private static List<string> ExtractTldCandidates(string domainName)
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
