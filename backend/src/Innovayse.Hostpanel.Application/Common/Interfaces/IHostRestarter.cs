namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Stops the host process the way a restart needs: it stops taking new requests, lets the running requests and background
/// jobs finish within the drain time and exits with the restart exit code, so the container's restart policy starts the
/// process again. Implemented in Infrastructure, where the host's lifetime is.
/// </summary>
public interface IHostRestarter
{
    /// <summary>Gets a value indicating whether the host has started to drain for a restart, which is what the health probe reports.</summary>
    public bool IsRestarting { get; }

    /// <summary>
    /// Asks for a restart and returns at once. The stop starts after a short pause, so the request that asked finishes and
    /// further plugin changes made in the meantime are applied by the same restart; a second call while a restart is
    /// pending changes nothing.
    /// </summary>
    public void Restart();
}
