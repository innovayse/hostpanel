namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>The registry JSON for a download ticket: the single-use address the package is fetched from.</summary>
/// <param name="Ticket">The ticket token; it is also part of <paramref name="DownloadUrl"/>.</param>
/// <param name="DownloadUrl">The address of the package with the ticket, absolute or relative to the registry address.</param>
internal sealed record PluginRegistryTicketWire(string? Ticket, string? DownloadUrl);
