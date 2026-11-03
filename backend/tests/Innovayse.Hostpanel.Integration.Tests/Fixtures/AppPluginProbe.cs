using System.Collections.Concurrent;
using System.Text;
using Innovayse.SDK.Plugins.Endpoints;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// What the test-only app plugins record about the calls they receive, and how they answer. One instance is
/// registered in the test host, so a test reads what the plugin saw and scripts what it says next, without the
/// plugin reaching for a static.
/// </summary>
public sealed class AppPluginProbe
{
    /// <summary>How many times a plugin was built.</summary>
    private int _constructed;

    /// <summary>Gets how many times a plugin was built; zero proves no plugin code, its constructor included, ran.</summary>
    public int Constructed => Volatile.Read(ref _constructed);

    /// <summary>Gets every call a plugin received, in order.</summary>
    public ConcurrentQueue<PluginCall> Calls { get; } = new();

    /// <summary>Gets or sets how a plugin answers a call; <see cref="Echo"/> until a test scripts another.</summary>
    public Func<PluginCall, PluginReply> Behaviour { get; set; } = Echo;

    /// <summary>Forgets the calls and the build count and restores <see cref="Echo"/>; each test starts from here.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _constructed, 0);
        Calls.Clear();
        Behaviour = Echo;
    }

    /// <summary>Notes that a plugin was built.</summary>
    public void NoteConstructed()
    {
        Interlocked.Increment(ref _constructed);
    }

    /// <summary>Answers with a JSON document describing the call, so a test reads what the plugin was given from the response.</summary>
    /// <param name="call">The call.</param>
    /// <returns>A 200 JSON reply.</returns>
    public static PluginReply Echo(PluginCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        return PluginReply.Json(new
        {
            area = call.Area.ToString(),
            method = call.Method.ToString(),
            route = call.Route,
            language = call.Language,
            clientRef = call.Caller.ClientRef,
            userRef = call.Caller.UserRef,
            permissions = call.Caller.Permissions,
            routeValues = call.RouteValues,
            query = call.Query,
            contentType = call.ContentType,
            body = Encoding.UTF8.GetString(call.Body.Span),
        });
    }
}
