namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>One page of the plugin registry, returned by <c>GET /api/admin/plugins/registry</c> and by the registry client.</summary>
/// <param name="Items">The plugins on this page.</param>
/// <param name="Total">How many plugins match the filters across all pages.</param>
/// <param name="Page">The 1-based page number.</param>
/// <param name="PageSize">The page size used.</param>
public sealed record RegistryPluginPageDto(IReadOnlyList<RegistryPluginDto> Items, int Total, int Page, int PageSize);
