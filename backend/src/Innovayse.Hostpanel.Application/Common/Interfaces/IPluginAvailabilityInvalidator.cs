namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Drops what the host remembers about which plugins are available to the panels (loaded, switched on and configured),
/// so the next index or page lookup reads the switches and settings again. Called by every use case that writes a
/// plugin's <c>integration:{id}:*</c> settings, after its save has committed, so a plugin the admin switches on or off
/// appears in or leaves the panels at once instead of after the cache lifetime.
/// </summary>
public interface IPluginAvailabilityInvalidator
{
    /// <summary>
    /// Forgets the remembered availability. Cheap and safe to call after any settings save, whether or not a plugin's
    /// availability actually changed; a lookup that was computing when this is called does not keep its result.
    /// </summary>
    void Invalidate();
}
