using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListRegistryPlugins;

/// <summary>
/// Lists one page of the plugin registry and marks which plugins are already installed here, at which version, and
/// whether a newer one is published.
/// </summary>
/// <param name="Q">Free text for the registry's own search (the <c>q</c> query parameter); <see langword="null"/> for none.</param>
/// <param name="Category">Only plugins in this registry category; <see langword="null"/> for every category.</param>
/// <param name="Page">The 1-based page number; a lower value reads as 1.</param>
/// <param name="PageSize">Plugins per page, clamped to the shared paging limits.</param>
public sealed record ListRegistryPluginsQuery(string? Q = null, PluginRegistryCategory? Category = null, int Page = 1, int PageSize = 20);
