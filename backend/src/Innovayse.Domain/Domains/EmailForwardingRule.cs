namespace Innovayse.Domain.Domains;

using Innovayse.Domain.Common;

/// <summary>
/// Represents an email forwarding rule that routes mail from a source alias
/// on the domain to an external destination address.
/// </summary>
public sealed class EmailForwardingRule : Entity
{
    /// <summary>Gets the FK to the owning domain.</summary>
    public int DomainId { get; private set; }

    /// <summary>Gets the source alias or local part (e.g. "info", "support").</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Gets the destination email address.</summary>
    public string Destination { get; private set; } = string.Empty;

    /// <summary>Gets whether this forwarding rule is currently active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>EF Core parameterless constructor — do not call directly.</summary>
    private EmailForwardingRule() : base(0) { }

    /// <summary>
    /// Creates a new email forwarding rule.
    /// </summary>
    /// <param name="domainId">FK to the owning domain.</param>
    /// <param name="source">Source alias or local part.</param>
    /// <param name="destination">Destination email address.</param>
    internal EmailForwardingRule(int domainId, string source, string destination) : base(0)
    {
        DomainId = domainId;
        Source = source;
        Destination = destination;
    }

    /// <summary>
    /// Updates this forwarding rule's properties.
    /// </summary>
    /// <param name="source">New source alias.</param>
    /// <param name="destination">New destination email.</param>
    /// <param name="isActive">Whether the rule is active.</param>
    internal void Update(string source, string destination, bool isActive)
    {
        Source = source;
        Destination = destination;
        IsActive = isActive;
    }
}
