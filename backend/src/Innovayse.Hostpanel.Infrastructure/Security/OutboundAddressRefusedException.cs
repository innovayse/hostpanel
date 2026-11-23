namespace Innovayse.Hostpanel.Infrastructure.Security;

/// <summary>
/// Thrown when a host an outbound client was asked to dial resolves only to addresses the
/// <see cref="Shared.Infrastructure.OutboundAddressPolicy"/> does not allow. An <see cref="IOException"/>, so every caller
/// that already treats an unreachable host as a connection failure handles it the same way.
/// </summary>
public sealed class OutboundAddressRefusedException : IOException
{
    /// <summary>Initializes a new instance of the <see cref="OutboundAddressRefusedException"/> class.</summary>
    /// <param name="host">The host that was refused; logged by the caller, never shown to a user.</param>
    public OutboundAddressRefusedException(string host)
        : base($"The address of '{host}' is not allowed for outbound connections.")
    {
    }
}
