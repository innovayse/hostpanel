namespace Innovayse.API.Domains.Requests;

/// <summary>Generic request payload for toggling a boolean domain setting (auto-renew, WHOIS privacy, registrar lock).</summary>
public sealed class SetBoolRequest
{
    /// <summary>Gets the desired enabled state for the setting.</summary>
    public required bool Enabled { get; init; }
}
