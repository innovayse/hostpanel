namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Opens the dependency scope a streamed plugin call owns. A stream is read by the response after the use case has
/// returned and its own scope, data context included, has been disposed; the SDK promises the plugin that its host API and
/// store stay valid until the stream ends, so a streamed call cannot live on the use case's scope.
/// </summary>
public interface IPluginCallScopeFactory
{
    /// <summary>Opens a new scope, independent of the request's and the use case's.</summary>
    /// <returns>The scope; whoever holds it disposes it once the call, or the stream it answered with, is over.</returns>
    public IPluginCallScope Open();
}
