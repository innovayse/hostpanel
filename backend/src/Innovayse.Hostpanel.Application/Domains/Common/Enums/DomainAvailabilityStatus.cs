namespace Innovayse.Hostpanel.Application.Domains.Common.Enums;

/// <summary>Whether a checked domain name can be registered. Written on the wire by name.</summary>
public enum DomainAvailabilityStatus
{
    /// <summary>Nobody holds the name; it can be registered.</summary>
    Available = 0,

    /// <summary>The name is already registered, here or at the registrar.</summary>
    Taken = 1,

    /// <summary>No registrar plugin sells the name's TLD, so it cannot be registered here.</summary>
    TldNotSupported = 2,
}
