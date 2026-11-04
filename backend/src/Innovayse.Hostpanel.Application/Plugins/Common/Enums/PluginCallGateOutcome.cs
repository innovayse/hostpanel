namespace Innovayse.Hostpanel.Application.Plugins.Common.Enums;

/// <summary>
/// What <see cref="PluginCallGate"/> decided about a plugin call. Every member except <see cref="Admitted"/> is a refusal, and
/// the reasons behind a plugin 404 differ here only so an operator can read them in the log: the response never tells them apart.
/// </summary>
public enum PluginCallGateOutcome
{
    /// <summary>The route is served, the caller may use it and the plugin is switched on and configured: the call may go on.</summary>
    Admitted = 1,

    /// <summary>The identity plugin that decides the caller's account did not answer (<c>IDENTITY_PLUGIN_UNAVAILABLE</c>).</summary>
    IdentityUnavailable = 2,

    /// <summary>The caller acts for no single account (<c>CLIENT_PROFILE_NOT_FOUND</c>).</summary>
    NoAccount = 3,

    /// <summary>No loaded serving plugin has that id (<c>PLUGIN_ROUTE_NOT_FOUND</c>).</summary>
    UnknownPlugin = 4,

    /// <summary>The plugin declares no route with that path in the area, whatever the method (<c>PLUGIN_ROUTE_NOT_FOUND</c>).</summary>
    UndeclaredRoute = 5,

    /// <summary>The plugin declares that path in the area, but not for that method (<c>PLUGIN_ROUTE_NOT_FOUND</c>).</summary>
    WrongMethod = 6,

    /// <summary>The route is declared and the caller does not hold its permission (<c>PLUGIN_ROUTE_NOT_FOUND</c>).</summary>
    PermissionMissing = 7,

    /// <summary>The plugin is switched off, lacks a required setting or cannot answer routes (<c>PLUGIN_ROUTE_NOT_FOUND</c>).</summary>
    Disabled = 8,
}
