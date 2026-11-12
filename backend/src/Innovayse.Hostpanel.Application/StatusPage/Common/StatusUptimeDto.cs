namespace Innovayse.Hostpanel.Application.StatusPage.Common;

/// <summary>The uptime the public status page shows: every component, in the page's order, over one window.</summary>
/// <param name="Days">The length of the window, in days.</param>
/// <param name="Components">The components in display order (groups first, then position), each with its uptime.</param>
public sealed record StatusUptimeDto(int Days, IReadOnlyList<StatusComponentUptimeDto> Components);
