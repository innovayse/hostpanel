using Innovayse.Hostpanel.Application.Admin.Plugins.Common;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary>One change record file: the change and whether the activity log already has a line for it.</summary>
/// <param name="Change">The change and how it ended.</param>
/// <param name="Reported">Whether the activity log has a line for the change.</param>
internal sealed record PluginChangeEntry(PluginChangeDto Change, bool Reported);
