using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>What the plugin registry is asked to list: one page of plugins that match the filters.</summary>
/// <param name="Search">Free text matched against the plugin name and description by the registry; <see langword="null"/> for no filter.</param>
/// <param name="Category">Only plugins in this category; <see langword="null"/> for every category.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">How many plugins a page holds.</param>
public sealed record RegistryPluginSearch(string? Search, PluginRegistryCategory? Category, int Page, int PageSize);
