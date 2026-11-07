using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>One area of the manifest <c>ui</c> block whose pages show views.</summary>
/// <param name="Area">The area whose <c>api</c> routes the pages may call.</param>
/// <param name="Member">The member name under <c>ui</c>, for the log.</param>
/// <param name="Ui">The area's pages.</param>
internal sealed record PageArea(PluginArea Area, string Member, PluginUiArea Ui);
