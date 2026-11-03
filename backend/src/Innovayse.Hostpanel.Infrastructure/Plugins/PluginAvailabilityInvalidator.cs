using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>
/// The <see cref="IPluginAvailabilityInvalidator"/> over the process's <see cref="PluginAvailabilityCache"/>. A separate
/// type registered by type, so the handlers that write plugin settings get it through a constructor call, with no
/// factory and no service location.
/// </summary>
public sealed class PluginAvailabilityInvalidator : IPluginAvailabilityInvalidator
{
    /// <summary>The remembered availability this drops.</summary>
    private readonly PluginAvailabilityCache _cache;

    /// <summary>Initializes a new instance of the <see cref="PluginAvailabilityInvalidator"/> class.</summary>
    /// <param name="cache">The remembered availability this drops.</param>
    public PluginAvailabilityInvalidator(PluginAvailabilityCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
        _cache.Invalidate();
    }
}
