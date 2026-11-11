namespace Innovayse.Hostpanel.Domain.StatusPage.Enums;

/// <summary>
/// What a visitor of the status page is told about one component. The numeric values run from the
/// calmest to the most severe state, so the worst state of a set of components is its maximum.
/// </summary>
public enum StatusComponentState
{
    /// <summary>The component works as expected.</summary>
    Operational = 1,

    /// <summary>The component is under planned maintenance.</summary>
    Maintenance = 2,

    /// <summary>The component works, but slower or with part of its features failing.</summary>
    Degraded = 3,

    /// <summary>The component is down.</summary>
    Outage = 4,
}
