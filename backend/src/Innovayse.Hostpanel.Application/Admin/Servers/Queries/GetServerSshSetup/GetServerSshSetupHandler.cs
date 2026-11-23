using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshSetup;

/// <summary>Handles <see cref="GetServerSshSetupQuery"/>.</summary>
public sealed class GetServerSshSetupHandler
{
    /// <summary>Data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Provider factory, for the agent profile.</summary>
    private readonly ISshKeyProviderFactory _factory;

    /// <summary>The bundles this build ships.</summary>
    private readonly ISshAgentBundles _bundles;

    /// <summary>How managed servers see this HostPanel.</summary>
    private readonly SshAgentOptions _network;

    /// <summary>Initialises the handler.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="factory">Provider factory, for the agent profile.</param>
    /// <param name="bundles">The bundles this build ships.</param>
    /// <param name="network">How managed servers see this HostPanel.</param>
    public GetServerSshSetupHandler(IHostpanelDbContext context, ISshKeyProviderFactory factory, ISshAgentBundles bundles, IOptions<SshAgentOptions> network)
    {
        _context = context;
        _factory = factory;
        _bundles = bundles;
        _network = network.Value;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The setup, or <c>SERVER_NOT_FOUND</c>.</returns>
    public async Task<Result<ServerSshSetupDto>> HandleAsync(GetServerSshSetupQuery query, CancellationToken ct)
    {
        var server = await _context.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == query.ServerId, ct);
        if (server is null)
        {
            return Result<ServerSshSetupDto>.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
        }

        var settings = await _context.ServerSshSettings.AsNoTracking().FirstOrDefaultAsync(s => s.ServerId == server.Id, ct);
        return Result<ServerSshSetupDto>.Success(settings.ToSetupDto(server.Id, _factory.AgentProfileFor(server.Module), _bundles, _network));
    }
}
