using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Integrations.Common;

/// <summary>The last known connection state of a plugin integration, as its card and detail page show it.</summary>
/// <param name="State">The state.</param>
/// <param name="Message">One sentence for the administrator in the request's language: the plugin's own detail, else the host's.</param>
/// <param name="CheckedAt">When the plugin was asked, UTC; a cached state keeps the moment of the check it came from.</param>
public sealed record IntegrationConnectionDto(IntegrationConnectionState State, string Message, DateTimeOffset CheckedAt);
