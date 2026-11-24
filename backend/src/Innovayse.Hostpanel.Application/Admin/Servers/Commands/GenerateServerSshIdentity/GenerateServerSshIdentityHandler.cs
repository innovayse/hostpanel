using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Admin.Servers.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.GenerateServerSshIdentity;

/// <summary>
/// Handles <see cref="GenerateServerSshIdentityCommand"/>: a new ED25519 pair for this server only.
/// The private half is stored encrypted and never leaves HostPanel; the public half goes into the
/// installer command. Rotating switches customer access off until the agent is reinstalled.
/// </summary>
public sealed class GenerateServerSshIdentityHandler
{
    /// <summary>The administrator, for the audit trail.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Logger; also the admin audit trail.</summary>
    private readonly ILogger<GenerateServerSshIdentityHandler> _logger;

    /// <summary>Data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Provider factory, for support and the agent profile.</summary>
    private readonly ISshKeyProviderFactory _factory;

    /// <summary>Key pair generator.</summary>
    private readonly ISshIdentityGenerator _generator;

    /// <summary>The bundles this build ships.</summary>
    private readonly ISshAgentBundles _bundles;

    /// <summary>How managed servers see this HostPanel.</summary>
    private readonly SshAgentOptions _network;

    /// <summary>Initialises the handler.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="factory">Provider factory, for support and the agent profile.</param>
    /// <param name="generator">Key pair generator.</param>
    /// <param name="bundles">The bundles this build ships.</param>
    /// <param name="network">How managed servers see this HostPanel.</param>
    /// <param name="caller">The administrator, for the audit trail.</param>
    /// <param name="logger">Logger; also the admin audit trail.</param>
    public GenerateServerSshIdentityHandler(
        IHostpanelDbContext context, ISshKeyProviderFactory factory, ISshIdentityGenerator generator, ISshAgentBundles bundles,
        IOptions<SshAgentOptions> network, ICurrentUserService caller, ILogger<GenerateServerSshIdentityHandler> logger)
    {
        _caller = caller;
        _logger = logger;
        _context = context;
        _factory = factory;
        _generator = generator;
        _bundles = bundles;
        _network = network.Value;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The setup with the new public key, or <c>SERVER_NOT_FOUND</c> / <c>SSH_MODULE_NOT_SUPPORTED</c>.</returns>
    public async Task<Result<ServerSshSetupDto>> HandleAsync(GenerateServerSshIdentityCommand cmd, CancellationToken ct)
    {
        var server = await _context.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cmd.ServerId, ct);
        if (server is null)
        {
            return Result<ServerSshSetupDto>.FailureCode(HostpanelErrorCodes.SERVER_NOT_FOUND);
        }

        var profile = _factory.AgentProfileFor(server.Module);
        if (profile is null)
        {
            return Result<ServerSshSetupDto>.FailureCode(HostpanelErrorCodes.SSH_MODULE_NOT_SUPPORTED);
        }

        var settings = await _context.ServerSshSettings.FirstOrDefaultAsync(s => s.ServerId == server.Id, ct);
        if (settings is null)
        {
            settings = ServerSshSettings.CreateFor(server.Id);
            _context.ServerSshSettings.Add(settings);
        }

        var identity = _generator.Generate($"hostpanel-server-{server.Id}");
        settings.IssueIdentity(identity.PrivateKey, identity.PublicKey);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "SSH identity of server {ServerId} issued by user {UserId}; SSH key management disabled until reinstall",
            server.Id, _caller.UserId);
        return Result<ServerSshSetupDto>.Success(settings.ToSetupDto(server.Id, profile, _bundles, _network));
    }
}
