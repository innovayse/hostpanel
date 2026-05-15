namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Represents a name server assigned to a domain.
/// Owned by the <see cref="Domain"/> aggregate — cannot exist independently.
/// </summary>
public sealed class Nameserver : Entity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the fully-qualified name server hostname (e.g. "ns1.example.com").</summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private Nameserver() : base(0) { }

    /// <summary>
    /// Creates a new nameserver entry owned by the specified domain.
    /// </summary>
    /// <param name="domainId">FK to the owning <see cref="Domain"/>.</param>
    /// <param name="host">Fully-qualified nameserver hostname.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="host"/> is null or whitespace.</exception>
    internal Nameserver(int domainId, string host) : base(0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        DomainId = domainId;
        Host = host;
    }
}
