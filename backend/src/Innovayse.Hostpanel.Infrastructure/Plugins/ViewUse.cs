using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>One place a view document is shown: the page or slot entry that names it and the area it lives in.</summary>
/// <param name="Path">The package-relative path the entry names.</param>
/// <param name="Area">The area whose <c>api</c> routes the document may call.</param>
/// <param name="Where">The entry, for the log; it names only manifest values and package-relative paths.</param>
internal readonly record struct ViewUse(string Path, PluginArea Area, string Where);
