namespace Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;

/// <summary>What a service connection test takes away from a seeded hosting service.</summary>
public enum ServiceDamage
{
    /// <summary>The service is no longer assigned to a server.</summary>
    NoServer,

    /// <summary>The service has no provisioning reference.</summary>
    NoProvisioningReference,

    /// <summary>The server the service is assigned to has been deleted.</summary>
    DeletedServer,
}
