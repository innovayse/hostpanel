namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>The invariants the status page entities share, stated once.</summary>
internal static class StatusPageRules
{
    /// <summary>Refuses an empty or whitespace display name.</summary>
    /// <param name="name">The display name to check.</param>
    /// <exception cref="DomainRuleException">The name is empty.</exception>
    internal static void EnsureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException(StatusPageErrors.NameRequired, "A name is required.");
        }
    }

    /// <summary>Refuses a negative position.</summary>
    /// <param name="displayOrder">The display order to check.</param>
    /// <exception cref="DomainRuleException">The display order is negative.</exception>
    internal static void EnsureDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
        {
            throw new DomainRuleException(StatusPageErrors.DisplayOrderNegative, "A display order cannot be negative.");
        }
    }
}
