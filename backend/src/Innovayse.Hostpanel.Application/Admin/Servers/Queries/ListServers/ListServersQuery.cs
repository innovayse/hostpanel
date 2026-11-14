using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServers;

/// <summary>Returns one page of the provisioning servers, narrowed by a search text, a module and a last known state.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only servers whose name, host name or IP address contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="Module">Optional control panel module filter. Null returns every module.</param>
/// <param name="State">Optional last known connection state filter. Null returns every state.</param>
public sealed record ListServersQuery(
    int Page,
    int PageSize,
    string? Search = null,
    ServerModule? Module = null,
    ServerState? State = null) : IListQuery;
