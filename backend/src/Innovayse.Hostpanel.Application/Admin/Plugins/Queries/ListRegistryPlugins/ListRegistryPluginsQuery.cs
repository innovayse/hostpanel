using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListRegistryPlugins;

/// <summary>
/// Lists one page of the plugin registry and marks which plugins are already installed here, at which version, and
/// whether a newer one is published.
/// </summary>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">Plugins per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Free text for the registry's own search; blank for none.</param>
/// <param name="Category">Only plugins in this registry category; <see langword="null"/> for every category.</param>
public sealed record ListRegistryPluginsQuery(int Page, int PageSize, string? Search = null, PluginRegistryCategory? Category = null) : IListQuery;
