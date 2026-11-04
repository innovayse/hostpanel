namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;

/// <summary>The connection state an integration card shows. Sent by name; values start at 1.</summary>
public enum IntegrationConnectionState
{
    /// <summary>The integration reports that it works.</summary>
    Connected = 1,

    /// <summary>The integration works with reduced function.</summary>
    Degraded = 2,

    /// <summary>The integration does not work: it said so, failed to answer or did not answer in time.</summary>
    Unavailable = 3,

    /// <summary>The integration is switched off or not fully configured, so it was not asked.</summary>
    Inactive = 4,
}
