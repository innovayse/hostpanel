namespace Innovayse.Domain.Clients;

/// <summary>Lifecycle status of a client account.</summary>
public enum ClientStatus
{
    /// <summary>Account is active and can use services.</summary>
    Active,

    /// <summary>Account is inactive (not yet activated or manually deactivated).</summary>
    Inactive,

    /// <summary>Account is suspended due to non-payment or policy violation.</summary>
    Suspended,

    /// <summary>Account is permanently closed.</summary>
    Closed
}
