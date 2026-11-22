namespace Innovayse.Hostpanel.Domain.Provisioning.Enums;

/// <summary>How a provider answered a request to create a hosting account.</summary>
public enum ProvisioningOutcome
{
    /// <summary>The account was created.</summary>
    Created = 0,

    /// <summary>
    /// An account under the requested username already exists on the panel, typically one an
    /// earlier attempt created before its answer was lost.
    /// </summary>
    AlreadyExists = 1,

    /// <summary>The panel refused to create the account.</summary>
    Refused = 2,

    /// <summary>
    /// The panel accepted the request and finishes creating the account on its own; the account reference is
    /// known but the account is not ready yet, so the service stays pending and a later attempt completes it.
    /// </summary>
    Pending = 3,
}
