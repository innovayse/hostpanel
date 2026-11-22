using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerModules;

/// <summary>Lists the control panel modules a server can be given, read from the module catalog.</summary>
public sealed class ListServerModulesHandler
{
    /// <summary>The control panel modules a server can be given.</summary>
    private readonly IServerModuleCatalog _modules;

    /// <summary>Initializes the handler.</summary>
    /// <param name="modules">The control panel modules a server can be given.</param>
    public ListServerModulesHandler(IServerModuleCatalog modules)
    {
        _modules = modules;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <returns>The modules, built in ones first. Never refuses.</returns>
    public Result<IReadOnlyList<ServerModuleOptionDto>> Handle(ListServerModulesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Result<IReadOnlyList<ServerModuleOptionDto>>.Success(_modules.List());
    }
}
